using System.Security.Claims;
using System.Text.Encodings.Web;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using SharpCrafters.Backstage.LicenseServer.Data;
using SharpCrafters.Backstage.LicenseServer.Email;
using SharpCrafters.Backstage.LicenseServer.Licensing;
using SharpCrafters.Backstage.LicenseServer.Locking;
using SharpCrafters.Backstage.LicenseServer.Options;
using SharpCrafters.Backstage.LicenseServer.Security;
using SharpCrafters.Backstage.LicenseServer.Tests.Fakes;

namespace SharpCrafters.Backstage.LicenseServer.Tests.Infrastructure;

/// <summary>
/// Hosts the real application in memory, with the SQL Server database swapped for SQLite and the
/// license parser, clock and email sender swapped for test doubles. Everything else -- routing,
/// model binding, authorization, the endpoints themselves -- is the production pipeline.
/// </summary>
public sealed class LicenseServerApplication : WebApplicationFactory<Program>
{
    private readonly SqliteConnection connection;

    private readonly string connectionString;

    public LicenseServerApplication()
    {
        // A shared-cache in-memory database, so that the application can open its own connections by
        // connection string while the database itself lives only as long as this one stays open.
        this.connectionString = $"DataSource=licenseserver-{Guid.NewGuid():N};Mode=Memory;Cache=Shared";

        this.connection = new SqliteConnection( this.connectionString );
        this.connection.Open();

        // Created up front, because a test seeds licenses before it issues its first request, which
        // is what actually starts the host.
        using LicenseServerDbContext db = this.CreateDbContext();
        db.Database.EnsureCreated();
    }

    public FakeLicenseParser LicenseParser { get; } = new();

    public InMemoryEmailSender EmailSender { get; } = new();

    /// <summary>
    /// Gets or sets the lock the lease endpoint uses, so that a test can force the overloaded path.
    /// </summary>
    public ILeaseLock LeaseLock { get; set; } = new InProcessLeaseLock();

    protected override void ConfigureWebHost( IWebHostBuilder builder )
    {
        builder.UseEnvironment( "Testing" );

        // UseSetting rather than ConfigureAppConfiguration: with the minimal hosting model the
        // application reads its configuration while Program.cs runs, which is before the
        // ConfigureAppConfiguration callbacks are applied.
        Dictionary<string, string?> settings = new()
        {
            ["LicenseServer:MachinesPerUser"] = "2",
            ["LicenseServer:NewLeaseDays"] = "3",
            ["LicenseServer:MinLeaseDays"] = "1",
            ["LicenseServer:BuildServers"] = "buildagent",
            ["LicenseServer:MutexTimeout"] = "5",
            ["LicenseServer:DatabaseProvider"] = "Sqlite",
            [$"ConnectionStrings:{DatabaseRegistration.ConnectionStringName}"] = this.connectionString,
            ["Smtp:Enabled"] = "false"
        };

        foreach ( (string key, string? value) in settings )
        {
            builder.UseSetting( key, value );
        }

        builder.ConfigureServices(
            services =>
            {
                // The database itself is not swapped here: the application selects SQLite from the
                // configuration above, through the same code path a customer would use.
                services.RemoveAll<ILicenseParser>();
                services.AddSingleton<ILicenseParser>( this.LicenseParser );

                services.RemoveAll<IEmailSender>();
                services.AddSingleton<IEmailSender>( this.EmailSender );

                services.RemoveAll<IAuditKeyProvider>();
                services.AddSingleton<IAuditKeyProvider, StaticAuditKeyProvider>();

                services.RemoveAll<ILeaseLock>();
                services.AddSingleton( _ => this.LeaseLock );

                // Windows authentication cannot be negotiated against an in-memory host.
                services.AddAuthentication( TestAuthenticationHandler.SchemeName )
                    .AddScheme<AuthenticationSchemeOptions, TestAuthenticationHandler>(
                        TestAuthenticationHandler.SchemeName,
                        _ => { } );

                services.PostConfigure<AuthenticationOptions>(
                    options =>
                    {
                        options.DefaultAuthenticateScheme = TestAuthenticationHandler.SchemeName;
                        options.DefaultChallengeScheme = TestAuthenticationHandler.SchemeName;
                    } );

                services.AddLogging( logging => logging.SetMinimumLevel( LogLevel.Warning ) );
            } );
    }

    public LicenseServerDbContext CreateDbContext()
        => new(
            new DbContextOptionsBuilder<LicenseServerDbContext>()
                .UseSqlite( this.connectionString )
                .Options );

    /// <summary>
    /// Registers a license both in the database and with the fake parser.
    /// </summary>
    public License AddLicense( LicenseBuilder builder )
    {
        LicenseInfo info = builder.BuildInfo();
        string key = $"FAKE-KEY-{info.LicenseId}";

        this.LicenseParser.Register( key, info );

        using LicenseServerDbContext db = this.CreateDbContext();

        License license = new()
        {
            LicenseId = info.LicenseId,
            LicenseKey = key,
            ProductCode = info.Product,
            CreatedOn = TestClock.Origin
        };

        db.Licenses.Add( license );
        db.SaveChanges();

        return license;
    }

    protected override void Dispose( bool disposing )
    {
        base.Dispose( disposing );

        if ( disposing )
        {
            this.connection.Dispose();
        }
    }
}

/// <summary>
/// Authenticates every request as the same Windows-style identity, so the tests exercise the
/// authenticated path without a domain controller.
/// </summary>
public sealed class TestAuthenticationHandler(
    IOptionsMonitor<AuthenticationSchemeOptions> options,
    ILoggerFactory logger,
    UrlEncoder encoder ) : AuthenticationHandler<AuthenticationSchemeOptions>( options, logger, encoder )
{
    public const string SchemeName = "Test";

    /// <summary>
    /// The header a test sets to be served anonymously instead.
    /// </summary>
    public const string AnonymousHeader = "X-Test-Anonymous";

    protected override Task<AuthenticateResult> HandleAuthenticateAsync()
    {
        if ( this.Request.Headers.ContainsKey( AnonymousHeader ) )
        {
            return Task.FromResult( AuthenticateResult.NoResult() );
        }

        ClaimsIdentity identity = new(
            [new Claim( ClaimTypes.Name, "DOMAIN\\tester" )],
            SchemeName );

        return Task.FromResult(
            AuthenticateResult.Success(
                new AuthenticationTicket( new ClaimsPrincipal( identity ), SchemeName ) ) );
    }
}
