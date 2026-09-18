using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using SharpCrafters.Backstage.LicenseServer.Data;

namespace SharpCrafters.Backstage.LicenseServer.Tests.Infrastructure;

/// <summary>
/// A SQLite database held in memory and created from the EF Core model. It behaves like a relational
/// database: it enforces the foreign keys, it runs transactions, and it translates the queries.
/// </summary>
/// <remarks>
/// A database held in memory exists as long as a connection to it is open, so this class holds one
/// connection during its whole lifetime. The cache is shared, so the code under test opens its own
/// connections to the same database, which is what a lock between two connections requires.
/// </remarks>
public sealed class SqliteTestDatabase : ITestDatabase
{
    private readonly SqliteConnection connection;

    private SqliteTestDatabase( SqliteConnection connection, string connectionString )
    {
        this.connection = connection;
        this.ConnectionString = connectionString;
    }

    public string ProviderName => "Sqlite";

    public string ConnectionString { get; }

    public static async Task<SqliteTestDatabase> CreateAsync()
    {
        string connectionString = $"DataSource=licenseserver-{Guid.NewGuid():N};Mode=Memory;Cache=Shared";

        SqliteConnection connection = new( connectionString );
        await connection.OpenAsync();

        SqliteTestDatabase database = new( connection, connectionString );

        await using LicenseServerDbContext context = database.CreateContext();
        await context.Database.EnsureCreatedAsync();

        return database;
    }

    public LicenseServerDbContext CreateContext()
        => new(
            new DbContextOptionsBuilder<LicenseServerDbContext>()
                .UseSqlite( this.ConnectionString )
                .EnableSensitiveDataLogging()
                .Options );

    /// <summary>
    /// Does nothing. This database belongs to one test and disappears with its connection, so no
    /// other test can see the schema that the test modified.
    /// </summary>
    public void DoNotReuse() { }

    public async ValueTask DisposeAsync() => await this.connection.DisposeAsync();
}
