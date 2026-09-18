// Copyright (c) SharpCrafters s.r.o. See the LICENSE.md file in the root directory of this repository root for details.

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
/// <see cref="Services.LeaseService"/> over the database of the test, with the license parser, the
/// clock and the e-mail sender replaced by test doubles.
/// </summary>
public sealed class LicenseServerTestContext : IAsyncDisposable
{
    private readonly ITestDatabase database;
    private readonly LicenseServerDbContext db;

    private LicenseServerTestContext(
        ITestDatabase database,
        LicenseServerDbContext db,
        LicenseServerOptions options )
    {
        this.database = database;
        this.db = db;
        this.Options = options;
        this.LicenseParser = new FakeLicenseParser();
        this.EmailSender = new InMemoryEmailSender();
        this.ServerVersion = new FixedServerVersion();

        this.Repository = new LeaseRepository(
            db,
            Microsoft.Extensions.Options.Options.Create( options ),
            this.LicenseParser );

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

    public FixedServerVersion ServerVersion { get; }

    public LeaseRepository Repository { get; }

    public LeaseService LeaseService { get; }

    public LicenseServerDbContext Db => this.db;

    /// <summary>
    /// Creates a context over a database that belongs to the calling test. See
    /// <see cref="TestDatabases"/> for the engine the run uses.
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

        var database = await TestDatabases.CreateAsync();

        return new LicenseServerTestContext( database, database.CreateContext(), options );
    }

    /// <summary>
    /// Returns a repository over a new unit of work on the same database. Use it to assert on the
    /// rows that were saved, and not on the state of the change tracker.
    /// </summary>
    public LeaseRepository CreateFreshRepository()
        => new(
            this.database.CreateContext(),
            Microsoft.Extensions.Options.Options.Create( this.Options ),
            this.LicenseParser );

    public LicenseServerDbContext CreateFreshContext() => this.database.CreateContext();

    /// <summary>
    /// States that the database of this context must not serve another test. A test that modifies the
    /// schema calls it, because a SQL Server run lends the same databases to one test after another.
    /// </summary>
    public void DoNotReuseDatabase() => this.database.DoNotReuse();

    public async ValueTask DisposeAsync()
    {
        await this.db.DisposeAsync();
        await this.database.DisposeAsync();
    }
}