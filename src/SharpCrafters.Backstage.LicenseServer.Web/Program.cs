using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using PostSharp.LicenseServer;
using PostSharp.LicenseServer.Data;
using PostSharp.LicenseServer.Email;
using PostSharp.LicenseServer.Endpoints;
using PostSharp.LicenseServer.Licensing;
using PostSharp.LicenseServer.Locking;
using PostSharp.LicenseServer.Options;
using PostSharp.LicenseServer.Security;
using PostSharp.LicenseServer.Services;
using PostSharp.LicenseServer.Time;

WebApplicationBuilder builder = WebApplication.CreateBuilder( args );

// The PostSharp SDK parses license keys, and has to be initialized once before it is first used.
PostSharpPlatform.EnsureInitialized();

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

builder.Services.AddSingleton<ILicenseParser>(
    _ => new CachingLicenseParser( new PostSharpLicenseParser() ) );

builder.Services.AddSingleton<ILicenseServerVersion, PostSharpServerVersion>();
builder.Services.AddSingleton<ILeaseSerializer, PostSharpLeaseSerializer>();

builder.Services.AddSingleton<IAuditKeyProvider>(
    services => new FileAuditKeyProvider(
        services.GetRequiredService<IOptions<LicenseServerOptions>>(),
        services.GetRequiredService<ILogger<FileAuditKeyProvider>>(),
        Path.Combine(
            services.GetRequiredService<IHostEnvironment>().ContentRootPath,
            "App_Data",
            "audit-signing.key" ) ) );

builder.Services.AddSingleton<ILeaseSigner, HmacLeaseSigner>();

builder.Services.AddSingleton<IEmailSender>(
    services => services.GetRequiredService<IOptions<SmtpOptions>>().Value.Enabled
        ? ActivatorUtilities.CreateInstance<SmtpEmailSender>( services )
        : new NullEmailSender() );

// Time acceleration exists so that a multi-day licensing scenario can be replayed in minutes. It is
// off unless explicitly configured.
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
                + "issue at https://github.com/postsharp/PostSharp.LicenseServer." ),
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

            // No roles configured means the administrative pages are open, which is how the legacy
            // Web.config shipped. Tightening this by default would lock administrators out of their
            // own server on upgrade. A warning is logged at startup instead.
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
app.MapLegacyUrlRedirects();

// On SQL Server the schema is created by Database/CreateTables.sql, which is the source of truth and
// which an administrator runs deliberately. A SQLite database is created on demand, because it is
// meant for evaluation and has no administrator to run a script.
if ( string.Equals(
        app.Configuration["LicenseServer:DatabaseProvider"],
        "Sqlite",
        StringComparison.OrdinalIgnoreCase ) )
{
    using IServiceScope scope = app.Services.CreateScope();
    scope.ServiceProvider.GetRequiredService<LicenseServerDbContext>().Database.EnsureCreated();
}

// The administrative pages are the only way to add or revoke a license, so an open default deserves
// more than a comment in a configuration file.
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

// Which scheme was chosen is worth stating, because leases recorded under "None" carry no user.
app.Logger.LogInformation( "Authenticating with the {Scheme} scheme.", authenticationScheme );

if ( authenticationScheme == AuthenticationRegistration.None )
{
    app.Logger.LogWarning(
        "Authentication is disabled, so leases will not record who requested them. Set {Setting} to "
        + "\"Negotiate\" on a host that is joined to your domain.",
        "Authentication:Scheme" );
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
/// Exposed so that integration tests can host the application in memory.
/// </summary>
public partial class Program;
