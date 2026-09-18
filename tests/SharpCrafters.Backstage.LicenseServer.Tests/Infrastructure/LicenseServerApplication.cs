// Copyright (c) SharpCrafters s.r.o. See the LICENSE.md file in the root directory of this repository root for details.

using System.Security.Claims;
using System.Text.Encodings.Web;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
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
using SharpCrafters.Backstage.LicenseServer.Tests.Fakes;
using SharpCrafters.Common;
using System.Globalization;

namespace SharpCrafters.Backstage.LicenseServer.Tests.Infrastructure;

/// <summary>
/// Hosts the real application in memory, over the database of the test, with test doubles for the
/// license parser, the clock and the e-mail sender. Everything else is the production
/// pipeline: the routing, the model binding, the authorization and the endpoints.
/// </summary>
public sealed class LicenseServerApplication : WebApplicationFactory<Program>
{
    private readonly ITestDatabase database;

    /// <summary>
    /// Creates an application over a database of its own, on the engine the run uses. The database is
    /// created here, and not at the first request, because a test adds licenses before it sends its
    /// first request, and the first request is what starts the host.
    /// </summary>
    /// <remarks>
    /// Creating the database is asynchronous, and a constructor cannot await. The work runs on the
    /// thread pool rather than on the synchronization context of the test, where waiting for it would
    /// deadlock. The alternative is an asynchronous factory, which every test class would have to
    /// call from <c>InitializeAsync</c>.
    /// </remarks>
    public LicenseServerApplication()
    {
        this.database = Task.Run( TestDatabases.CreateAsync ).GetAwaiter().GetResult();
    }

    public FakeLicenseParser LicenseParser { get; } = new();

    public InMemoryEmailSender EmailSender { get; } = new();

    /// <summary>
    /// Gets or sets a lock that replaces the one of the engine, so that a test can force the path
    /// that answers "Service overloaded.". When it stays null, the application uses the lock of its
    /// database engine, as it does in production.
    /// </summary>
    public ILeaseLock? LeaseLock { get; set; }

    /// <summary>
    /// Gets the provider of the synchronization points, which lets a test hold a request at a named
    /// point in the code under test. It is registered in every test and enabled by none.
    /// </summary>
    public TestSynchronizationProvider Synchronization { get; } = new();

    /// <summary>
    /// Gets the guard that makes the response body refuse a synchronous write, which a test writing
    /// to the response body enables.
    /// </summary>
    public AsyncOnlyResponseBody ResponseBody { get; } = new();

    /// <summary>
    /// Gets or sets the number of seconds a request waits for the lease lock, which a test that
    /// exercises the timeout shortens.
    /// </summary>
    public int MutexTimeoutSeconds { get; set; } = 5;

    protected override void ConfigureWebHost( IWebHostBuilder builder )
    {
        builder.UseEnvironment( "Testing" );

        // The settings are passed with UseSetting and not with ConfigureAppConfiguration. With the
        // minimal hosting model, the application reads its configuration while Program.cs runs,
        // which is before the callbacks of ConfigureAppConfiguration are applied.
        Dictionary<string, string?> settings = new()
        {
            ["LicenseServer:MachinesPerUser"] = "2",
            ["LicenseServer:NewLeaseDays"] = "3",
            ["LicenseServer:MinLeaseDays"] = "1",
            ["LicenseServer:BuildServers"] = "buildagent",
            ["LicenseServer:MutexTimeout"] = this.MutexTimeoutSeconds.ToString( CultureInfo.InvariantCulture ),
            ["LicenseServer:DatabaseProvider"] = this.database.ProviderName,
            [$"ConnectionStrings:{DatabaseRegistration.ConnectionStringName}"] = this.database.ConnectionString,
            ["Smtp:Enabled"] = "false"
        };

        foreach ( var (key, value) in settings )
        {
            builder.UseSetting( key, value );
        }

        builder.ConfigureServices( services =>
        {
            // Neither the database nor its lock is replaced here. The application selects both
            // from the configuration above, through the code path that a customer uses.
            services.RemoveAll<ILicenseParser>();
            services.AddSingleton<ILicenseParser>( this.LicenseParser );

            services.RemoveAll<IEmailSender>();
            services.AddSingleton<IEmailSender>( this.EmailSender );

            if ( this.LeaseLock != null )
            {
                services.RemoveAll<ILeaseLock>();
                services.AddSingleton( this.LeaseLock );
            }

            services.AddSingleton<ITestSynchronizationProvider>( this.Synchronization );

            // Windows authentication cannot be negotiated against an in-memory host.
            services.AddAuthentication( TestAuthenticationHandler.SchemeName )
                .AddScheme<AuthenticationSchemeOptions, TestAuthenticationHandler>(
                    TestAuthenticationHandler.SchemeName,
                    _ => { } );

            services.PostConfigure<AuthenticationOptions>( options =>
            {
                options.DefaultAuthenticateScheme = TestAuthenticationHandler.SchemeName;
                options.DefaultChallengeScheme = TestAuthenticationHandler.SchemeName;
            } );

            services.AddSingleton<IStartupFilter>( this.ResponseBody );

            services.AddLogging( logging => logging.SetMinimumLevel( LogLevel.Warning ) );
        } );
    }

    public LicenseServerDbContext CreateDbContext() => this.database.CreateContext();

    /// <summary>
    /// States that the database of this application must not serve another test. A test that modifies
    /// the schema calls it, because a SQL Server run lends the same databases to one test after
    /// another.
    /// </summary>
    public void DoNotReuseDatabase() => this.database.DoNotReuse();

    /// <summary>
    /// Registers a license both in the database and with the fake parser.
    /// </summary>
    public License AddLicense( LicenseBuilder builder )
    {
        var info = builder.BuildInfo();
        var key = $"FAKE-KEY-{info.LicenseId}";

        this.LicenseParser.Register( key, info );

        using var db = this.CreateDbContext();

        License license = new()
        {
            LicenseId = info.LicenseId,
            LicenseKey = key,
            ProductCode = info.Product,
            Priority = builder.Priority,
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
            this.Synchronization.Dispose();

            // The database of a SQL Server run returns to its pool here, and a SQLite database in
            // memory disappears with its connection. Disposal runs on the thread pool for the same
            // reason as the creation.
            Task.Run( async () => await this.database.DisposeAsync() ).GetAwaiter().GetResult();
        }
    }
}