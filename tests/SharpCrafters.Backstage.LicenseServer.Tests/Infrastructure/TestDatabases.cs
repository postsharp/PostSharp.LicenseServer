using SharpCrafters.Backstage.LicenseServer.Data;

namespace SharpCrafters.Backstage.LicenseServer.Tests.Infrastructure;

/// <summary>
/// A database that belongs to one test, on the engine the test run was started with.
/// </summary>
public interface ITestDatabase : IAsyncDisposable
{
    /// <summary>
    /// Gets the name of the provider, as <c>LicenseServer:DatabaseProvider</c> spells it.
    /// </summary>
    string ProviderName { get; }

    /// <summary>
    /// Gets the connection string of this database, which the tests that host the whole application
    /// pass to it as configuration.
    /// </summary>
    string ConnectionString { get; }

    /// <summary>
    /// Creates a context over this database. Each context is a separate unit of work with its own
    /// change tracker, so a test can distinguish a lease that is pending from a lease that is saved.
    /// </summary>
    LicenseServerDbContext CreateContext();

    /// <summary>
    /// States that this database must not serve another test, which a test that modifies the schema
    /// calls. A SQL Server run lends its databases to one test after another, and a test that drops
    /// a table would leave the next test without one.
    /// </summary>
    void DoNotReuse();
}

/// <summary>
/// Creates the database of a test on the engine the run was started with.
/// </summary>
/// <remarks>
/// <para>
/// The engine is a property of the run and not of a test, so the same tests run against both. SQLite
/// is the default, because it needs no server and keeps the development loop short. The run uses SQL
/// Server when <c>LICENSESERVER_TEST_SQLSERVER</c> holds a connection string, which is how the
/// continuous integration build runs the suite a second time against the engine that customers use.
/// </para>
/// <para>
/// Start that server with <c>docker compose up -d database</c>, which is the same service the
/// container deployment uses.
/// </para>
/// </remarks>
public static class TestDatabases
{
    /// <summary>
    /// The name of the environment variable that holds the connection string of the SQL Server used
    /// by the tests. The connection string names no database: each test receives one of its own.
    /// </summary>
    public const string SqlServerVariable = "LICENSESERVER_TEST_SQLSERVER";

    /// <summary>
    /// Gets the connection string of the SQL Server of the run, or <c>null</c> when the run uses
    /// SQLite.
    /// </summary>
    public static string? SqlServerConnectionString { get; } =
        Environment.GetEnvironmentVariable( SqlServerVariable ) is { Length: > 0 } value ? value : null;

    /// <summary>
    /// Gets a value indicating whether this run uses SQL Server.
    /// </summary>
    public static bool UsesSqlServer => SqlServerConnectionString != null;

    public static async Task<ITestDatabase> CreateAsync()
        => SqlServerConnectionString == null
            ? await SqliteTestDatabase.CreateAsync()
            : await SqlServerTestDatabase.CreateAsync( SqlServerConnectionString );
}
