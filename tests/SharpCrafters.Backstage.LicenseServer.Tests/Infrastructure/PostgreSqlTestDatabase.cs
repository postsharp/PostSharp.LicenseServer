// Copyright (c) SharpCrafters s.r.o. See the LICENSE.md file in the root directory of this repository root for details.

using System.Collections.Concurrent;
using System.Diagnostics.CodeAnalysis;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using SharpCrafters.Backstage.LicenseServer.Data;

namespace SharpCrafters.Backstage.LicenseServer.Tests.Infrastructure;

/// <summary>
/// A database of the PostgreSQL server that the run was given, created from
/// <c>CreateTables.PostgreSql.sql</c>.
/// </summary>
/// <remarks>
/// <para>
/// The schema comes from the script that an administrator runs, and not from the EF Core model, so
/// this run also proves that the model and the script agree.
/// </para>
/// <para>
/// The databases are pooled, for the reason the SQL Server databases are pooled: a test takes one,
/// the tables are emptied, and the database returns to the pool when the test ends.
/// </para>
/// </remarks>
public sealed class PostgreSqlTestDatabase : ITestDatabase
{
    private readonly PostgreSqlDatabasePool pool;
    private readonly string databaseName;
    private int returned;
    private volatile bool reusable = true;

    private PostgreSqlTestDatabase( PostgreSqlDatabasePool pool, string databaseName, string connectionString )
    {
        this.pool = pool;
        this.databaseName = databaseName;
        this.ConnectionString = connectionString;
    }

    public string ProviderName => "PostgreSql";

    public string ConnectionString { get; }

    public static async Task<PostgreSqlTestDatabase> CreateAsync( string serverConnectionString )
    {
        var pool = PostgreSqlDatabasePool.Get( serverConnectionString );
        var databaseName = await pool.RentAsync();

        return new PostgreSqlTestDatabase( pool, databaseName, pool.GetConnectionString( databaseName ) );
    }

    public LicenseServerDbContext CreateContext()
        => new(
            new DbContextOptionsBuilder<LicenseServerDbContext>()
                .UseNpgsql( this.ConnectionString )
                .EnableSensitiveDataLogging()
                .Options );

    /// <summary>
    /// Marks this database for deletion instead of reuse, which a test that modifies the schema calls.
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