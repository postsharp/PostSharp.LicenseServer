using System.Runtime.InteropServices;
using System.Text.Encodings.Web;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Negotiate;
using Microsoft.AspNetCore.Server.IISIntegration;
using Microsoft.Extensions.Options;

namespace SharpCrafters.Backstage.LicenseServer;

/// <summary>
/// Chooses how the license server identifies the person borrowing a license.
/// </summary>
public static class AuthenticationRegistration
{
    /// <summary>
    /// Windows authentication handled by IIS. Required for in-process hosting behind IIS.
    /// </summary>
    public const string IisIntegrated = "IISIntegrated";

    /// <summary>
    /// Windows authentication handled by the application. Works on Windows out of the box, and on
    /// other systems once the host is joined to the domain and has a Kerberos keytab.
    /// </summary>
    public const string Negotiate = "Negotiate";

    /// <summary>
    /// No authentication. Leases are recorded with an empty authenticated user, exactly as an
    /// anonymous request is already recorded today.
    /// </summary>
    public const string None = "None";

    /// <summary>
    /// Registers the scheme named by <c>Authentication:Scheme</c>.
    /// </summary>
    /// <returns>The scheme that was registered, for logging.</returns>
    public static string AddLicenseServerAuthentication(
        this IServiceCollection services,
        IConfiguration configuration )
    {
        string scheme = ResolveScheme( configuration );

        switch ( scheme )
        {
            case IisIntegrated:
                services.AddAuthentication( IISDefaults.AuthenticationScheme );

                break;

            case Negotiate:
                services.AddAuthentication( NegotiateDefaults.AuthenticationScheme ).AddNegotiate();

                break;

            case None:
                services.AddAuthentication( AnonymousAuthenticationHandler.SchemeName )
                    .AddScheme<AuthenticationSchemeOptions, AnonymousAuthenticationHandler>(
                        AnonymousAuthenticationHandler.SchemeName,
                        _ => { } );

                break;

            default:
                throw new InvalidOperationException(
                    $"Unknown authentication scheme '{scheme}'. Use '{IisIntegrated}', '{Negotiate}' or '{None}'." );
        }

        return scheme;
    }

    /// <summary>
    /// Works out which scheme to use when the configuration does not say.
    /// </summary>
    /// <remarks>
    /// The same package runs behind IIS, on a Windows machine under its own process, and on a Linux
    /// host that may have no domain at all. Guessing wrong is quiet rather than loud -- the server
    /// would simply record every lease against an empty user -- so the choice is made from how the
    /// application is actually being hosted, and is logged at startup.
    /// </remarks>
    private static string ResolveScheme( IConfiguration configuration )
    {
        string? configured = configuration["Authentication:Scheme"];

        if ( !string.IsNullOrWhiteSpace( configured ) )
        {
            // Accept any casing, so that "negotiate" in a container's environment works.
            foreach ( string known in new[] { IisIntegrated, Negotiate, None } )
            {
                if ( string.Equals( configured, known, StringComparison.OrdinalIgnoreCase ) )
                {
                    return known;
                }
            }

            return configured;
        }

        // The ASP.NET Core Module sets this when it hosts the application.
        if ( Environment.GetEnvironmentVariable( "ASPNETCORE_IIS_PHYSICAL_PATH" ) != null )
        {
            return IisIntegrated;
        }

        return RuntimeInformation.IsOSPlatform( OSPlatform.Windows ) ? Negotiate : None;
    }
}

/// <summary>
/// Authenticates nobody, so that requests are served anonymously.
/// </summary>
public sealed class AnonymousAuthenticationHandler(
    IOptionsMonitor<AuthenticationSchemeOptions> options,
    ILoggerFactory logger,
    UrlEncoder encoder ) : AuthenticationHandler<AuthenticationSchemeOptions>( options, logger, encoder )
{
    public const string SchemeName = "None";

    protected override Task<AuthenticateResult> HandleAuthenticateAsync()
        => Task.FromResult( AuthenticateResult.NoResult() );
}
