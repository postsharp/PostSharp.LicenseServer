using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using SharpCrafters.Backstage.LicenseServer.Data;

namespace SharpCrafters.Backstage.LicenseServer.Tests.Infrastructure;

/// <summary>
/// A SQLite database held in memory and created from the EF Core model. It behaves like a relational
/// database: it enforces the foreign keys, it runs transactions, and it translates the queries.
/// </summary>
/// <remarks>
/// A SQLite database held in memory exists as long as a connection to it is open, so this fixture
/// holds one connection during its whole lifetime and returns contexts that share that connection.
/// Each context is a separate unit of work with its own change tracker, so a test can distinguish a
/// lease that is pending from a lease that is saved.
/// </remarks>
public sealed class SqliteDatabaseFixture : IAsyncDisposable
{
    private readonly SqliteConnection connection;

    private SqliteDatabaseFixture( SqliteConnection connection )
    {
        this.connection = connection;
    }

    public SqliteConnection Connection => this.connection;

    public static async Task<SqliteDatabaseFixture> CreateAsync()
    {
        SqliteConnection connection = new( "DataSource=:memory:" );
        await connection.OpenAsync();

        SqliteDatabaseFixture fixture = new( connection );

        await using LicenseServerDbContext context = fixture.CreateContext();
        await context.Database.EnsureCreatedAsync();

        return fixture;
    }

    public LicenseServerDbContext CreateContext()
        => new(
            new DbContextOptionsBuilder<LicenseServerDbContext>()
                .UseSqlite( this.connection )
                .EnableSensitiveDataLogging()
                .Options );

    public async ValueTask DisposeAsync() => await this.connection.DisposeAsync();
}
