using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using SharpCrafters.Backstage.LicenseServer.Data;
using SharpCrafters.Backstage.LicenseServer.Licensing;
using SharpCrafters.Backstage.LicenseServer.Options;
using SharpCrafters.Backstage.LicenseServer.Services;
using SharpCrafters.Backstage.LicenseServer.Tests.Fakes;

namespace SharpCrafters.Backstage.LicenseServer.Tests.Infrastructure;

/// <summary>
/// A license server wired up for a test: a real <see cref="LeaseRepository"/> and
/// <see cref="Services.LeaseService"/> over an in-memory database, with the license parser, the
/// clock, the signer and the email sender replaced by test doubles.
/// </summary>
public sealed class LicenseServerTestContext : IAsyncDisposable
{
    private readonly SqliteDatabaseFixture fixture;
    private readonly LicenseServerDbContext db;

    private LicenseServerTestContext(
        SqliteDatabaseFixture fixture,
        LicenseServerDbContext db,
        LicenseServerOptions options )
    {
        this.fixture = fixture;
        this.db = db;
        this.Options = options;
        this.LicenseParser = new FakeLicenseParser();
        this.EmailSender = new InMemoryEmailSender();
        this.Signer = new RecordingLeaseSigner();
        this.ServerVersion = new FixedServerVersion();

        this.Repository = new LeaseRepository(
            db,
            Microsoft.Extensions.Options.Options.Create( options ),
            this.LicenseParser,
            this.Signer );

        this.LeaseService = new LeaseService(
            this.Repository,
            Microsoft.Extensions.Options.Options.Create( options ),
            this.LicenseParser,
            this.ServerVersion,
            this.EmailSender,
            NullLogger<LeaseService>.Instance );
    }

    public LicenseServerOptions Options { get; }

    public FakeLicenseParser LicenseParser { get; }

    public InMemoryEmailSender EmailSender { get; }

    public RecordingLeaseSigner Signer { get; }

    public FixedServerVersion ServerVersion { get; }

    public LeaseRepository Repository { get; }

    public LeaseService LeaseService { get; }

    public LicenseServerDbContext Db => this.db;

    /// <summary>
    /// Creates a context over a database that is private to the calling test. Creating an in-memory
    /// SQLite database takes well under a millisecond, so there is no reason to share one between
    /// tests and then have to reset it.
    /// </summary>
    public static async Task<LicenseServerTestContext> CreateAsync( Action<LicenseServerOptions>? configure = null )
    {
        LicenseServerOptions options = new()
        {
            MachinesPerUser = 2,
            NewLeaseDays = 3,
            MinLeaseDays = 1,
            GracePeriodWarningDays = 1,
            GracePeriodWarningEmailTo = "admin@example.com",
            DeniedRequestEmailTo = "admin@example.com"
        };

        configure?.Invoke( options );

        SqliteDatabaseFixture fixture = await SqliteDatabaseFixture.CreateAsync();

        return new LicenseServerTestContext( fixture, fixture.CreateContext(), options );
    }

    /// <summary>
    /// Returns a repository over a fresh unit of work, sharing the same database. Use this to assert
    /// on what was actually persisted, rather than on what the change tracker remembers.
    /// </summary>
    public LeaseRepository CreateFreshRepository()
        => new(
            this.fixture.CreateContext(),
            Microsoft.Extensions.Options.Options.Create( this.Options ),
            this.LicenseParser,
            this.Signer );

    public LicenseServerDbContext CreateFreshContext() => this.fixture.CreateContext();

    public async ValueTask DisposeAsync()
    {
        await this.db.DisposeAsync();
        await this.fixture.DisposeAsync();
    }
}
