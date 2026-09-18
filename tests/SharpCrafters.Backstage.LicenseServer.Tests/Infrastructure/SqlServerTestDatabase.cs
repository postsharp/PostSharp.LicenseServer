using System.Collections.Concurrent;
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
        SqlServerDatabasePool pool = SqlServerDatabasePool.Get( serverConnectionString );
        string databaseName = await pool.RentAsync();

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

/// <summary>
/// The databases of one SQL Server, lent to the tests one at a time.
/// </summary>
internal sealed class SqlServerDatabasePool
{
    /// <summary>
    /// The prefix of every database this pool creates. A run drops the databases that carry it before
    /// it creates its own, so a run that was interrupted leaves nothing behind for long.
    /// </summary>
    private const string prefix = "licenseserver_test_";

    /// <summary>
    /// The number of databases the pool lends at the same time, which is also the number of tests
    /// that touch the server at the same time. A test that finds no free database waits for one.
    /// Without this bound, every test of the suite would create a database of its own at once, and
    /// the server would refuse the connections.
    /// </summary>
    private const int capacity = 8;

    private static readonly ConcurrentDictionary<string, SqlServerDatabasePool> pools = new( StringComparer.Ordinal );

    private readonly string serverConnectionString;
    private readonly ConcurrentBag<string> available = [];
    private readonly SemaphoreSlim lease = new( capacity, capacity );
    private readonly SemaphoreSlim initialization = new( 1, 1 );
    private bool initialized;

    private SqlServerDatabasePool( string serverConnectionString )
    {
        SqlConnectionStringBuilder builder = new( serverConnectionString );

        // Creating a database and connecting to it while the suite runs takes longer than the
        // fifteen seconds of the default.
        if ( builder.ConnectTimeout < 60 )
        {
            builder.ConnectTimeout = 60;
        }

        this.serverConnectionString = builder.ToString();
    }

    public static SqlServerDatabasePool Get( string serverConnectionString )
        => pools.GetOrAdd( serverConnectionString, connectionString => new SqlServerDatabasePool( connectionString ) );

    public string GetConnectionString( string databaseName )
        => new SqlConnectionStringBuilder( this.serverConnectionString ) { InitialCatalog = databaseName }.ToString();

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

            await ExecuteAsync( this.serverConnectionString, $"CREATE DATABASE [{databaseName}]" );
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
    /// used by an earlier test looks like a database that was just created. The first lease of the
    /// next test therefore always receives the identifier 1.
    /// </summary>
    /// <remarks>
    /// The reseed is conditional. On a table that has received a row since it was created, the next
    /// row takes the new value plus the increment, which is 1. On a table that has received no row,
    /// the next row takes the new value itself, which is 0, and a test that asserts on the identifier
    /// of a lease then fails. The column <c>last_value</c> is null until the first row is inserted,
    /// and that case needs no reseed, because the next row already takes the seed of the column.
    /// </remarks>
    private async Task ClearAsync( string databaseName )
        => await ExecuteAsync(
            this.GetConnectionString( databaseName ),
            """
            DELETE FROM [dbo].[Leases];
            DELETE FROM [dbo].[Licenses];

            IF EXISTS (
                SELECT 1 FROM sys.identity_columns
                WHERE object_id = OBJECT_ID('[dbo].[Leases]') AND last_value IS NOT NULL )
            BEGIN
                DBCC CHECKIDENT ('[dbo].[Leases]', RESEED, 0) WITH NO_INFOMSGS;
            END
            """ );

    private async Task CreateSchemaAsync( string databaseName )
    {
        string script = await File.ReadAllTextAsync( LocateCreateTablesScript() );

        // sqlcmd separates the batches of a script with GO, which is not a statement of Transact-SQL.
        foreach ( string batch in script.Split(
                     "\nGO",
                     StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries ) )
        {
            if ( batch.Length > 0 )
            {
                await ExecuteAsync( this.GetConnectionString( databaseName ), batch );
            }
        }
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

            await using SqlConnection connection = new( this.serverConnectionString );
            await connection.OpenAsync();

            List<string> leftovers = [];

            await using ( SqlCommand query = connection.CreateCommand() )
            {
                query.CommandText = $"SELECT name FROM sys.databases WHERE name LIKE '{prefix}%'";

                await using SqlDataReader reader = await query.ExecuteReaderAsync();

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
    /// Drops one database. A test leaves its connections open in the pool of the client, so the
    /// server closes them before it drops the database.
    /// </summary>
    private async Task DropAsync( string databaseName )
        => await ExecuteAsync(
            this.serverConnectionString,
            $"""
             ALTER DATABASE [{databaseName}] SET SINGLE_USER WITH ROLLBACK IMMEDIATE;
             DROP DATABASE [{databaseName}];
             """ );

    private static async Task ExecuteAsync( string connectionString, string sql )
    {
        await using SqlConnection connection = new( connectionString );
        await connection.OpenAsync();

        await using SqlCommand command = connection.CreateCommand();
        command.CommandText = sql;

        await command.ExecuteNonQueryAsync();
    }

    /// <summary>
    /// Finds <c>CreateTables.sql</c> next to the test assembly, where the web project copies it.
    /// </summary>
    private static string LocateCreateTablesScript()
    {
        string path = Path.Combine( AppContext.BaseDirectory, "Database", "CreateTables.sql" );

        if ( !File.Exists( path ) )
        {
            throw new FileNotFoundException(
                $"The schema script was not found at '{path}'. The test project copies it from the web project.",
                path );
        }

        return path;
    }
}
