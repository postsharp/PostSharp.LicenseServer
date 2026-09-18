using System.Data.Common;
using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using SharpCrafters.Backstage.LicenseServer.Data;
using SharpCrafters.Backstage.LicenseServer.Locking;

namespace SharpCrafters.Backstage.LicenseServer.Web.Locking;

/// <summary>
/// Serializes the lease requests with an advisory lock of PostgreSQL, so that the lock covers every
/// process connected to the database.
/// </summary>
/// <remarks>
/// <para>
/// The lock belongs to the session, which means to the connection. The connection is therefore opened
/// here and stays open until the handle is disposed. Without that, Entity Framework would close the
/// connection after the first query and the lock would be released in the middle of the request. The
/// server would keep answering, and it would grant more leases than the capacity of the license.
/// </para>
/// <para>
/// The wait is bounded by <c>lock_timeout</c>. When it elapses, PostgreSQL cancels the statement with
/// the code 55P03, and the caller answers with the status 503, as it does on SQL Server.
/// </para>
/// </remarks>
public sealed class PostgreSqlLeaseLock( LicenseServerDbContext db ) : ILeaseLock
{
    /// <summary>
    /// The name of the locked resource, which is the name the SQL Server lock uses. The two engines
    /// never share a database, so the two locks never meet.
    /// </summary>
    private const string resourceName = "SharpCrafters.Backstage.LicenseServer.Lease";

    /// <summary>
    /// The code PostgreSQL reports when <c>lock_timeout</c> elapses, which is <c>lock_not_available</c>.
    /// </summary>
    private const string lockNotAvailable = "55P03";

    /// <summary>
    /// The key of the advisory lock. PostgreSQL identifies an advisory lock by a number and not by a
    /// name, so the number is derived from <see cref="resourceName"/>. An advisory lock is scoped to
    /// the database, so every process that serves this database asks for this key, and nothing else
    /// does.
    /// </summary>
    public static readonly long ResourceKey =
        BitConverter.ToInt64( SHA256.HashData( Encoding.UTF8.GetBytes( resourceName ) ) );

    public async ValueTask<IAsyncDisposable?> TryAcquireAsync(
        TimeSpan timeout,
        CancellationToken cancellationToken = default )
    {
        DatabaseFacade database = db.Database;

        await database.OpenConnectionAsync( cancellationToken );

        try
        {
            // SET takes no parameter, so the value is written into the statement. It is a number
            // computed here and never a value that a request carries.
            int milliseconds = Math.Max( 0, (int) timeout.TotalMilliseconds );

            await ExecuteAsync(
                database,
                $"SET lock_timeout = {milliseconds.ToString( CultureInfo.InvariantCulture )}",
                cancellationToken );

            try
            {
                await ExecuteAsync( database, $"SELECT pg_advisory_lock({ResourceKey})", cancellationToken );
            }
            catch ( DbException e ) when ( e.SqlState == lockNotAvailable )
            {
                await ResetTimeoutAsync( database );
                await database.CloseConnectionAsync();

                return null;
            }

            return new Handle( database );
        }
        catch
        {
            await database.CloseConnectionAsync();

            throw;
        }
    }

    private static async Task ExecuteAsync(
        DatabaseFacade database,
        string sql,
        CancellationToken cancellationToken )
    {
        await using DbCommand command = database.GetDbConnection().CreateCommand();
        command.CommandText = sql;

        await command.ExecuteNonQueryAsync( cancellationToken );
    }

    /// <summary>
    /// Restores the timeout of the session. The connection returns to the pool of the client, which
    /// hands it to another request.
    /// </summary>
    private static async Task ResetTimeoutAsync( DatabaseFacade database )
        => await ExecuteAsync( database, "SET lock_timeout = DEFAULT", CancellationToken.None );

    private sealed class Handle( DatabaseFacade database ) : IAsyncDisposable
    {
        private int released;

        public async ValueTask DisposeAsync()
        {
            if ( Interlocked.Exchange( ref this.released, 1 ) != 0 )
            {
                return;
            }

            try
            {
                await ExecuteAsync(
                    database,
                    $"SELECT pg_advisory_unlock({ResourceKey})",
                    CancellationToken.None );

                await ResetTimeoutAsync( database );
            }
            finally
            {
                // Closing the connection releases the lock as well, so the lock is never held by a
                // connection that returns to the pool.
                await database.CloseConnectionAsync();
            }
        }
    }
}
