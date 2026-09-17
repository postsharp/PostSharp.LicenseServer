using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using PostSharp.LicenseServer.Data;

namespace PostSharp.LicenseServer.Tests.Infrastructure;

/// <summary>
/// An in-memory SQLite database, created from the EF Core model, that behaves like a real relational
/// database: foreign keys, transactions, and server-side query translation all apply.
/// </summary>
/// <remarks>
/// A SQLite in-memory database lives exactly as long as a connection to it is open, so this fixture
/// holds one connection for its whole lifetime and hands out contexts that share it. Each context is
/// a separate unit of work with its own change tracker, which is what lets a test tell a pending
/// lease apart from a committed one.
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
