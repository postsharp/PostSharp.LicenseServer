using System.Collections.Concurrent;
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
        PostgreSqlDatabasePool pool = PostgreSqlDatabasePool.Get( serverConnectionString );
        string databaseName = await pool.RentAsync();

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

/// <summary>
/// The databases of one PostgreSQL server, lent to the tests one at a time.
/// </summary>
internal sealed class PostgreSqlDatabasePool
{
    /// <summary>
    /// The prefix of every database this pool creates. A run drops the databases that carry it before
    /// it creates its own, so a run that was interrupted leaves nothing behind for long.
    /// </summary>
    private const string prefix = "licenseserver_test_";

    /// <summary>
    /// The number of databases the pool lends at the same time, which is also the number of tests
    /// that touch the server at the same time. A test that finds no free database waits for one.
    /// </summary>
    private const int capacity = 8;

    private static readonly ConcurrentDictionary<string, PostgreSqlDatabasePool> pools = new( StringComparer.Ordinal );

    private readonly string serverConnectionString;
    private readonly ConcurrentBag<string> available = [];
    private readonly SemaphoreSlim lease = new( capacity, capacity );
    private readonly SemaphoreSlim initialization = new( 1, 1 );
    private bool initialized;

    private PostgreSqlDatabasePool( string serverConnectionString )
    {
        NpgsqlConnectionStringBuilder builder = new( serverConnectionString );

        // A connection string that names no database connects to the maintenance database, which is
        // where CREATE DATABASE and DROP DATABASE run.
        if ( string.IsNullOrEmpty( builder.Database ) )
        {
            builder.Database = "postgres";
        }

        this.serverConnectionString = builder.ToString();
    }

    public static PostgreSqlDatabasePool Get( string serverConnectionString )
        => pools.GetOrAdd( serverConnectionString, connectionString => new PostgreSqlDatabasePool( connectionString ) );

    public string GetConnectionString( string databaseName )
        => new NpgsqlConnectionStringBuilder( this.serverConnectionString ) { Database = databaseName }.ToString();

    public async Task<string> RentAsync()
    {
        await this.DropLeftoverDatabasesAsync();
        await this.lease.WaitAsync();

        try
        {
            if ( this.available.TryTake( out string? databaseName ) )
            {
                await this.ClearAsync( databaseName );

                return databaseName;
            }

            databaseName = prefix + Guid.NewGuid().ToString( "N" );

            await ExecuteAsync( this.serverConnectionString, $"CREATE DATABASE \"{databaseName}\"" );
            await this.CreateSchemaAsync( databaseName );

            return databaseName;
        }
        catch
        {
            this.lease.Release();

            throw;
        }
    }

    /// <summary>
    /// Takes a database back. A database whose schema a test modified is dropped instead of kept, and
    /// the next test that finds the pool empty creates one.
    /// </summary>
    public async ValueTask ReturnAsync( string databaseName, bool reusable )
    {
        try
        {
            if ( reusable )
            {
                this.available.Add( databaseName );
            }
            else
            {
                await this.DropAsync( databaseName );
            }
        }
        finally
        {
            this.lease.Release();
        }
    }

    /// <summary>
    /// Empties the two tables and restarts the identity of the leases, so that a database that was
    /// used by an earlier test looks like a database that was just created. TRUNCATE states both, and
    /// it states them for a table a foreign key points to, which is why it names both tables.
    /// </summary>
    private async Task ClearAsync( string databaseName )
        => await ExecuteAsync(
            this.GetConnectionString( databaseName ),
            "TRUNCATE TABLE \"Leases\", \"Licenses\" RESTART IDENTITY" );

    private async Task CreateSchemaAsync( string databaseName )
    {
        string script = await File.ReadAllTextAsync( LocateCreateTablesScript() );

        // PostgreSQL has no batch separator: the whole script is one command.
        await ExecuteAsync( this.GetConnectionString( databaseName ), script );
    }

    /// <summary>
    /// Drops the databases that an interrupted run left behind. It runs once per pool, before the
    /// first database is created.
    /// </summary>
    private async Task DropLeftoverDatabasesAsync()
    {
        if ( this.initialized )
        {
            return;
        }

        await this.initialization.WaitAsync();

        try
        {
            if ( this.initialized )
            {
                return;
            }

            List<string> leftovers = [];

            await using ( NpgsqlConnection connection = new( this.serverConnectionString ) )
            {
                await connection.OpenAsync();

                await using NpgsqlCommand query = connection.CreateCommand();
                query.CommandText = $"SELECT datname FROM pg_database WHERE datname LIKE '{prefix}%'";

                await using NpgsqlDataReader reader = await query.ExecuteReaderAsync();

                while ( await reader.ReadAsync() )
                {
                    leftovers.Add( reader.GetString( 0 ) );
                }
            }

            foreach ( string leftover in leftovers )
            {
                await this.DropAsync( leftover );
            }

            this.initialized = true;
        }
        finally
        {
            this.initialization.Release();
        }
    }

    /// <summary>
    /// Drops one database. A test leaves its connections open in the pool of the client, and
    /// PostgreSQL refuses to drop a database that a session is connected to, so the connections of
    /// this client are closed first and the remaining sessions are ended by the server.
    /// </summary>
    private async Task DropAsync( string databaseName )
    {
        NpgsqlConnection.ClearPool( new NpgsqlConnection( this.GetConnectionString( databaseName ) ) );

        await ExecuteAsync(
            this.serverConnectionString,
            $"""
             SELECT pg_terminate_backend(pid) FROM pg_stat_activity
             WHERE datname = '{databaseName}' AND pid <> pg_backend_pid();
             """ );

        await ExecuteAsync( this.serverConnectionString, $"DROP DATABASE IF EXISTS \"{databaseName}\"" );
    }

    private static async Task ExecuteAsync( string connectionString, string sql )
    {
        await using NpgsqlConnection connection = new( connectionString );
        await connection.OpenAsync();

        await using NpgsqlCommand command = connection.CreateCommand();
        command.CommandText = sql;

        await command.ExecuteNonQueryAsync();
    }

    /// <summary>
    /// Finds <c>CreateTables.PostgreSql.sql</c> next to the test assembly, where the web project
    /// copies it.
    /// </summary>
    private static string LocateCreateTablesScript()
    {
        string path = Path.Combine( AppContext.BaseDirectory, "Database", "CreateTables.PostgreSql.sql" );

        if ( !File.Exists( path ) )
        {
            throw new FileNotFoundException(
                $"The schema script was not found at '{path}'. The test project copies it from the web project.",
                path );
        }

        return path;
    }
}
