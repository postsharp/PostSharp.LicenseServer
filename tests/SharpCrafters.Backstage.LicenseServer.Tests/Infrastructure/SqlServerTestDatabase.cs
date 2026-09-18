// Copyright (c) SharpCrafters s.r.o. See the LICENSE.md file in the root directory of this repository root for details.

using System.Collections.Concurrent;
using System.Diagnostics.CodeAnalysis;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using SharpCrafters.Backstage.LicenseServer.Data;

namespace SharpCrafters.Backstage.LicenseServer.Tests.Infrastructure;

/// <summary>
/// A database of the SQL Server that the run was given, created from <c>CreateTables.sql</c>.
/// </summary>
/// <remarks>
/// <para>
/// The schema comes from the script that an administrator runs, and not from the EF Core model, so
/// this run also proves that the model and the script agree.
/// </para>
/// <para>
/// Creating a database costs about half a second, and the suite has several hundred tests, so the
/// databases are pooled. A test takes one, the tables are emptied, and the database returns to the
/// pool when the test ends. The pool therefore holds as many databases as the number of tests that
/// run at the same time.
/// </para>
/// </remarks>
public sealed class SqlServerTestDatabase : ITestDatabase
{
    private readonly SqlServerDatabasePool pool;
    private readonly string databaseName;
    private int returned;
    private volatile bool reusable = true;

    private SqlServerTestDatabase( SqlServerDatabasePool pool, string databaseName, string connectionString )
    {
        this.pool = pool;
        this.databaseName = databaseName;
        this.ConnectionString = connectionString;
    }

    public string ProviderName => "SqlServer";

    public string ConnectionString { get; }

    public static async Task<SqlServerTestDatabase> CreateAsync( string serverConnectionString )
    {
        var pool = SqlServerDatabasePool.Get( serverConnectionString );
        var databaseName = await pool.RentAsync();

        return new SqlServerTestDatabase( pool, databaseName, pool.GetConnectionString( databaseName ) );
    }

    public LicenseServerDbContext CreateContext()
        => new(
            new DbContextOptionsBuilder<LicenseServerDbContext>()
                .UseSqlServer( this.ConnectionString )
                .EnableSensitiveDataLogging()
                .Options );

    /// <summary>
    /// Marks this database for deletion instead of reuse. The pool empties the tables of a database
    /// before it lends it again, and a test that dropped a table would leave the next test without
    /// one.
    /// </summary>
    public void DoNotReuse() => this.reusable = false;

    /// <summary>
    /// Returns the database to the pool, once. A host that implements both <see cref="IDisposable"/>
    /// and <see cref="IAsyncDisposable"/> can dispose its database twice, and the pool would then
    /// lend the same database to two tests, which clear it under each other.
    /// </summary>
    public async ValueTask DisposeAsync()
    {
        if ( Interlocked.Exchange( ref this.returned, 1 ) == 0 )
        {
            await this.pool.ReturnAsync( this.databaseName, this.reusable );
        }
    }
}