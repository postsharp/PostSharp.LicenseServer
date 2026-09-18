// Copyright (c) SharpCrafters s.r.o. See the LICENSE.md file in the root directory of this repository root for details.

using System.Data;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Storage;
using SharpCrafters.Backstage.LicenseServer.Data;
using SharpCrafters.Backstage.LicenseServer.Locking;

namespace SharpCrafters.Backstage.LicenseServer.Web.Locking;

/// <summary>
/// Serializes the lease requests with a write transaction of SQLite.
/// </summary>
/// <remarks>
/// <para>
/// SQLite has no application lock. A transaction opened as <c>BEGIN IMMEDIATE</c> takes the write
/// lock of the database at once, and SQLite allows one writer at a time, so a second request waits.
/// The transaction covers the whole request, and Entity Framework runs its own statements inside it,
/// so the reads that decide the allocation and the insert that records it are one unit.
/// </para>
/// <para>
/// The wait is bounded by the timeout of the caller. When it elapses, SQLite reports that the
/// database is locked, and the caller answers with the status 503, as it does on SQL Server.
/// </para>
/// </remarks>
public sealed class SqliteLeaseLock : ILeaseLock
{
    private readonly LicenseServerDbContext db;

    public SqliteLeaseLock( LicenseServerDbContext db )
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
            var connection = (SqliteConnection) database.GetDbConnection();

            // Microsoft.Data.Sqlite retries a statement that finds the database locked, and it stops
            // after DefaultTimeout, which is thirty seconds. The PRAGMA busy_timeout of SQLite does
            // not change that, because the provider passes its own value at every command. The
            // previous value is restored with the connection, which the pool hands to another
            // request.
            var previousTimeout = connection.DefaultTimeout;
            connection.DefaultTimeout = Math.Max( 1, (int) timeout.TotalSeconds );

            SqliteTransaction transaction;

            try
            {
                // deferred: false is BEGIN IMMEDIATE, which takes the write lock now instead of at the
                // first write. Without it, two requests would both read, and one would fail at its
                // insert instead of waiting for its turn.
                //
                // Microsoft.Data.Sqlite offers no asynchronous form of this call, and the wait for the
                // write lock therefore blocks this thread. SQLite serves the development loop and an
                // evaluation, where one user sends one request at a time.
                transaction = connection.BeginTransaction( IsolationLevel.Serializable, deferred: false );
            }
            catch ( SqliteException e ) when ( e.SqliteErrorCode is SqliteBusy or SqliteLocked )
            {
                connection.DefaultTimeout = previousTimeout;
                await database.CloseConnectionAsync();

                return null;
            }

            // The context runs its own statements inside this transaction, so the reads that decide
            // the allocation and the insert that records it are one unit.
            var contextTransaction =
                await database.UseTransactionAsync( transaction, cancellationToken );

            return new Handle( database, contextTransaction!, connection, previousTimeout );
        }
        catch
        {
            await database.CloseConnectionAsync();

            throw;
        }
    }

    private const int SqliteBusy = 5;
    private const int SqliteLocked = 6;

    private sealed class Handle : IAsyncDisposable
    {
        private readonly DatabaseFacade database;
        private readonly IDbContextTransaction transaction;
        private readonly SqliteConnection connection;
        private readonly int previousTimeout;
        private int released;

        public Handle(
            DatabaseFacade database,
            IDbContextTransaction transaction,
            SqliteConnection connection,
            int previousTimeout )
        {
            this.database = database;
            this.transaction = transaction;
            this.connection = connection;
            this.previousTimeout = previousTimeout;
        }

        public async ValueTask DisposeAsync()
        {
            if ( Interlocked.Exchange( ref this.released, 1 ) != 0 )
            {
                return;
            }

            try
            {
                // The lease was saved inside this this.transaction, so the commit is what makes it
                // visible, and it releases the write lock.
                await this.transaction.CommitAsync();
            }
            finally
            {
                await this.transaction.DisposeAsync();
                this.connection.DefaultTimeout = this.previousTimeout;
                await this.database.CloseConnectionAsync();
            }
        }
    }
}