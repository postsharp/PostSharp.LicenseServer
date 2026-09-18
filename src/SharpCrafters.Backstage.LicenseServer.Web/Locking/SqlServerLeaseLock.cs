// Copyright (c) SharpCrafters s.r.o. See the LICENSE.md file in the root directory of this repository root for details.

using System.Data;
using System.Data.Common;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using SharpCrafters.Backstage.LicenseServer.Data;
using SharpCrafters.Backstage.LicenseServer.Locking;

namespace SharpCrafters.Backstage.LicenseServer.Web.Locking;

/// <summary>
/// Serializes the lease requests with an application lock of SQL Server, so that the lock covers
/// every process connected to the database.
/// </summary>
/// <remarks>
/// <para>
/// The lock belongs to the session, which means to the connection. The connection is therefore opened
/// here and stays open until the handle is disposed. Without that, Entity Framework would close the
/// connection after the first query and the lock would be released in the middle of the request. The
/// server would keep answering, and it would grant more leases than the capacity of the license.
/// </para>
/// <para>
/// Every process that serves the same database asks for the same resource name, so the lock
/// serializes the requests of a web garden and of several instances as well.
/// </para>
/// </remarks>
public sealed class SqlServerLeaseLock : ILeaseLock
{
    private readonly LicenseServerDbContext db;

    /// <summary>
    /// The name of the locked resource. SQL Server scopes an application lock to the database, so
    /// this name is shared by every process that serves this database, and by nothing else.
    /// </summary>
    public const string ResourceName = "SharpCrafters.Backstage.LicenseServer.Lease";

    public SqlServerLeaseLock( LicenseServerDbContext db )
    {
        this.db = db;
    }

    public async ValueTask<IAsyncDisposable?> TryAcquireAsync(
        TimeSpan timeout,
        CancellationToken cancellationToken = default )
    {
        var database = this.db.Database;

        await database.OpenConnectionAsync( cancellationToken );

        try
        {
            var result = await ExecuteAsync(
                database,
                "sp_getapplock",
                command =>
                {
                    AddParameter( command, "@Resource", ResourceName );
                    AddParameter( command, "@LockMode", "Exclusive" );
                    AddParameter( command, "@LockOwner", "Session" );
                    AddParameter( command, "@LockTimeout", (int) timeout.TotalMilliseconds );
                },
                cancellationToken );

            // 0 and 1 mean granted. -1 means that the timeout elapsed, which the caller answers with
            // the status 503. Anything else is a fault of the server or of the arguments.
            if ( result == -1 )
            {
                await database.CloseConnectionAsync();

                return null;
            }

            if ( result < 0 )
            {
                await database.CloseConnectionAsync();

                throw new InvalidOperationException( $"sp_getapplock returned {result} for the resource '{ResourceName}'." );
            }

            return new Handle( database );
        }
        catch
        {
            await database.CloseConnectionAsync();

            throw;
        }
    }

    private static void AddParameter( DbCommand command, string name, object value )
    {
        var parameter = command.CreateParameter();
        parameter.ParameterName = name;
        parameter.Value = value;
        command.Parameters.Add( parameter );
    }

    /// <summary>
    /// Runs one of the two stored procedures and returns its return value, which is how both report
    /// their result.
    /// </summary>
    private static async Task<int> ExecuteAsync(
        DatabaseFacade database,
        string procedure,
        Action<DbCommand> addParameters,
        CancellationToken cancellationToken )
    {
        await using var command = database.GetDbConnection().CreateCommand();

        command.CommandText = procedure;
        command.CommandType = CommandType.StoredProcedure;

        addParameters( command );

        var returnValue = command.CreateParameter();
        returnValue.ParameterName = "@Result";
        returnValue.DbType = DbType.Int32;
        returnValue.Direction = ParameterDirection.ReturnValue;
        command.Parameters.Add( returnValue );

        await command.ExecuteNonQueryAsync( cancellationToken );

        return (int) returnValue.Value!;
    }

    private sealed class Handle : IAsyncDisposable
    {
        private readonly DatabaseFacade database;
        private int released;

        public Handle( DatabaseFacade database )
        {
            this.database = database;
        }

        public async ValueTask DisposeAsync()
        {
            if ( Interlocked.Exchange( ref this.released, 1 ) != 0 )
            {
                return;
            }

            try
            {
                await ExecuteAsync(
                    this.database,
                    "sp_releaseapplock",
                    command =>
                    {
                        AddParameter( command, "@Resource", ResourceName );
                        AddParameter( command, "@LockOwner", "Session" );
                    },
                    CancellationToken.None );
            }
            finally
            {
                // Closing the connection releases the lock as well, so the lock is never held by a
                // connection that returns to the pool.
                await this.database.CloseConnectionAsync();
            }
        }
    }
}