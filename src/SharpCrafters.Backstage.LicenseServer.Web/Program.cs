using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using SharpCrafters.Backstage.LicenseServer;
using SharpCrafters.Backstage.LicenseServer.Data;
using SharpCrafters.Backstage.LicenseServer.Email;
using SharpCrafters.Backstage.LicenseServer.Endpoints;
using SharpCrafters.Backstage.LicenseServer.Health;
using SharpCrafters.Backstage.LicenseServer.Licensing;
using SharpCrafters.Backstage.LicenseServer.Locking;
using SharpCrafters.Backstage.LicenseServer.Options;
using SharpCrafters.Backstage.LicenseServer.Services;
using SharpCrafters.Backstage.LicenseServer.Time;

WebApplicationBuilder builder = WebApplication.CreateBuilder( args );

builder.Services
    .AddOptions<LicenseServerOptions>()
    .Bind( builder.Configuration.GetSection( LicenseServerOptions.SectionName ) )
    .ValidateDataAnnotations()
    .ValidateOnStart();

builder.Services.AddSingleton<IValidateOptions<LicenseServerOptions>, LicenseServerOptionsValidator>();

builder.Services
    .AddOptions<SmtpOptions>()
    .Bind( builder.Configuration.GetSection( SmtpOptions.SectionName ) )
    .ValidateDataAnnotations()
    .ValidateOnStart();

builder.Services.AddLicenseServerDatabase( builder.Configuration, builder.Environment );

builder.Services.AddScoped<ILeaseRepository, LeaseRepository>();
builder.Services.AddScoped<LeaseService>();

IReadOnlyList<byte> testLicensingAuthorities =
    builder.Services.AddLicenseServerLicensing( builder.Configuration, builder.Environment );

builder.Services.AddSingleton<ILicenseServerVersion, BackstageServerVersion>();

builder.Services.AddScoped<LicenseAvailabilityService>();

// The health checks of the monitoring probe. The liveness probe at /health/live runs none of them.
// See OperationsEndpoints.
builder.Services.AddHealthChecks()
    .AddCheck<DatabaseHealthCheck>( "database" )
    .AddCheck<LicenseHealthCheck>( "licenses" );

builder.Services.AddSingleton<ILeaseSerializer, LeaseSerializer>();

builder.Services.AddSingleton<IEmailSender>(
    services => services.GetRequiredService<IOptions<SmtpOptions>>().Value.Enabled
        ? ActivatorUtilities.CreateInstance<SmtpEmailSender>( services )
        : new NullEmailSender() );

// The acceleration of the clock exists so that a licensing scenario that lasts several days can be
// replayed in minutes. It is disabled unless the configuration enables it.
builder.Services.AddSingleton<TimeProvider>(
    services =>
    {
        LicenseServerOptions options = services.GetRequiredService<IOptions<LicenseServerOptions>>().Value;

        if ( options.TimeAcceleration is 0 or 1 )
        {
            return TimeProvider.System;
        }

        services.GetRequiredService<ILogger<Program>>()
            .LogWarning(
                "Time is accelerated by a factor of {Acceleration}. This is a test configuration and must not be "
                + "used in production.",
                options.TimeAcceleration );

        return new AcceleratedTimeProvider( TimeProvider.System, (double) options.TimeAcceleration );
    } );

builder.Services.AddSingleton<ILeaseLock>(
    services =>
    {
        LicenseServerOptions options = services.GetRequiredService<IOptions<LicenseServerOptions>>().Value;

        return options.LeaseLockMode switch
        {
            LeaseLockMode.InProcess => new InProcessLeaseLock(),
            LeaseLockMode.None => new NullLeaseLock(),
            LeaseLockMode.SqlApplicationLock => throw new NotSupportedException(
                "LeaseLockMode.SqlApplicationLock is not implemented yet. Run a single worker process, or open an "
                + "issue at https://github.com/postsharp/SharpCrafters.Backstage.LicenseServer." ),
            _ => throw new InvalidOperationException( $"Unknown lease lock mode '{options.LeaseLockMode}'." )
        };
    } );

string authenticationScheme = builder.Services.AddLicenseServerAuthentication( builder.Configuration );

builder.Services.AddAuthorizationBuilder()
    .AddPolicy(
        AuthorizationPolicies.Admin,
        policy =>
        {
            string[] roles = builder.Configuration
                .GetSection( $"{LicenseServerOptions.SectionName}:AdminRoles" )
                .Get<string[]>() ?? [];

            // When no role is configured, the administrative pages are open, as they were in the
            // legacy Web.config. A restrictive default would lock administrators out of their own
            // server during an upgrade. The server writes a warning to the log at startup instead.
            if ( roles.Length == 0 )
            {
                policy.RequireAssertion( _ => true );
            }
            else
            {
                policy.RequireRole( roles );
            }
        } )
    .AddPolicy(
        AuthorizationPolicies.LeaseRequest,
        policy =>
        {
            bool requireAuthentication = builder.Configuration.GetValue(
                $"{LicenseServerOptions.SectionName}:RequireAuthenticatedLeaseRequests",
                false );

            if ( requireAuthentication )
            {
                policy.RequireAuthenticatedUser();
            }
            else
            {
                policy.RequireAssertion( _ => true );
            }
        } );

builder.Services.AddRazorPages(
    options =>
    {
        options.Conventions.AuthorizeFolder( "/Admin", AuthorizationPolicies.Admin );
    } );

WebApplication app = builder.Build();

if ( !app.Environment.IsDevelopment() )
{
    app.UseExceptionHandler( "/Error" );
}

app.UseStaticFiles();
app.UseRouting();
app.UseAuthentication();
app.UseAuthorization();

app.MapRazorPages();
app.MapLicenseServerEndpoints();
app.MapOperationsEndpoints();
app.MapLegacyUrlRedirects();

// On SQL Server, Database/CreateTables.sql creates the schema. That script defines the schema, and
// an administrator runs it. A SQLite database is created here, because it is used for evaluation and
// for tests, where no administrator runs a script.
if ( string.Equals(
        app.Configuration["LicenseServer:DatabaseProvider"],
        "Sqlite",
        StringComparison.OrdinalIgnoreCase ) )
{
    using IServiceScope scope = app.Services.CreateScope();
    scope.ServiceProvider.GetRequiredService<LicenseServerDbContext>().Database.EnsureCreated();
}

// A development server can issue to itself the license keys it serves, so that an evaluation or a
// load simulation has a license to lease. The registration has already refused to start when this
// setting is used outside the Development environment.
{
    TestLicenseAuthority? testAuthority = app.Services.GetService<TestLicenseAuthority>();

    if ( testAuthority != null )
    {
        using IServiceScope scope = app.Services.CreateScope();

        TestLicenseSeeder.Seed(
            scope.ServiceProvider.GetRequiredService<LicenseServerDbContext>(),
            testAuthority,
            app.Services.GetRequiredService<TimeProvider>(),
            app.Logger );
    }
}

// The administrative pages are the only way to add and to revoke a license. The server writes a
// warning, because a comment in a configuration file is not enough for an open default.
{
    LicenseServerOptions options = app.Services.GetRequiredService<IOptions<LicenseServerOptions>>().Value;

    if ( options.AdminRoles.Length == 0 )
    {
        app.Logger.LogWarning(
            "The administrative pages are not restricted. Set {Setting} to the Windows groups allowed to manage "
            + "licenses, for example \"DOMAIN\\\\PostSharp Administrators\".",
            $"{LicenseServerOptions.SectionName}:AdminRoles" );
    }
}

// The server writes the selected scheme to the log, because the leases recorded under the scheme
// "None" carry no authenticated user.
app.Logger.LogInformation( "Authenticating with the {Scheme} scheme.", authenticationScheme );

if ( authenticationScheme == AuthenticationRegistration.None )
{
    app.Logger.LogWarning(
        "Authentication is disabled, so leases will not record who requested them. Set {Setting} to "
        + "\"Negotiate\" on a host that is joined to your domain.",
        "Authentication:Scheme" );
}

if ( testLicensingAuthorities.Count > 0 )
{
    app.Logger.LogWarning(
        "This server accepts license keys signed by the test licensing authorities {KeyIds} besides the "
        + "production one. This is a test configuration and must not be used in production.",
        string.Join( ", ", testLicensingAuthorities ) );
}

app.Run();

/// <summary>
/// Names of the authorization policies.
/// </summary>
public static class AuthorizationPolicies
{
    public const string Admin = "Admin";
    public const string LeaseRequest = "LeaseRequest";
}

/// <summary>
/// Declared as a public type so that the integration tests can host the application in memory.
/// </summary>
public partial class Program;
