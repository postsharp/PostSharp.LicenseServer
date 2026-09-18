// Copyright (c) SharpCrafters s.r.o. See the LICENSE.md file in the root directory of this repository root for details.

using SharpCrafters.Backstage.LicenseServer.Data;

namespace SharpCrafters.Backstage.LicenseServer.Tests.Infrastructure;

/// <summary>
/// Creates the database of a test on the engine the run was started with.
/// </summary>
/// <remarks>
/// <para>
/// The engine is a property of the run and not of a test, so the same tests run against all three.
/// SQLite is the default, because it needs no server and keeps the development loop short. The run
/// uses SQL Server when <c>LICENSESERVER_TEST_SQLSERVER</c> holds a connection string, and PostgreSQL
/// when <c>LICENSESERVER_TEST_POSTGRESQL</c> holds one. That is how the continuous integration build
/// runs the suite again against each engine that customers use.
/// </para>
/// <para>
/// A Docker test starts either server in a container and sets the variable. See <c>tests/docker</c> and
/// the section "Running the tests" of <c>README.md</c>.
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
    /// The name of the environment variable that holds the connection string of the PostgreSQL server
    /// used by the tests. The connection string names no database: each test receives one of its own.
    /// </summary>
    public const string PostgreSqlVariable = "LICENSESERVER_TEST_POSTGRESQL";

    /// <summary>
    /// Gets the connection string of the SQL Server of the run, or <c>null</c> when the run uses
    /// another engine.
    /// </summary>
    public static string? SqlServerConnectionString { get; } = Read( SqlServerVariable );

    /// <summary>
    /// Gets the connection string of the PostgreSQL server of the run, or <c>null</c> when the run
    /// uses another engine.
    /// </summary>
    public static string? PostgreSqlConnectionString { get; } = Read( PostgreSqlVariable );

    /// <summary>
    /// Gets a value indicating whether this run uses SQL Server.
    /// </summary>
    public static bool UsesSqlServer => SqlServerConnectionString != null;

    /// <summary>
    /// Gets a value indicating whether this run uses PostgreSQL.
    /// </summary>
    public static bool UsesPostgreSql => PostgreSqlConnectionString != null;

    /// <summary>
    /// Gets a value indicating whether this run uses SQLite, which is the engine of a run that was
    /// given no other one.
    /// </summary>
    public static bool UsesSqlite => !UsesSqlServer && !UsesPostgreSql;

    public static async Task<ITestDatabase> CreateAsync()
    {
        if ( SqlServerConnectionString != null )
        {
            return await SqlServerTestDatabase.CreateAsync( SqlServerConnectionString );
        }

        if ( PostgreSqlConnectionString != null )
        {
            return await PostgreSqlTestDatabase.CreateAsync( PostgreSqlConnectionString );
        }

        return await SqliteTestDatabase.CreateAsync();
    }

    private static string? Read( string variable ) => Environment.GetEnvironmentVariable( variable ) is { Length: > 0 } value ? value : null;
}