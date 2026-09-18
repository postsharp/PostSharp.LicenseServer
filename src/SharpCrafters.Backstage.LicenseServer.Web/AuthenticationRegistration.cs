// Copyright (c) SharpCrafters s.r.o. See the LICENSE.md file in the root directory of this repository root for details.

using System.Runtime.InteropServices;
using System.Text.Encodings.Web;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Negotiate;
using Microsoft.AspNetCore.Server.IISIntegration;
using Microsoft.Extensions.Options;

namespace SharpCrafters.Backstage.LicenseServer;

/// <summary>
/// Selects how the license server authenticates the caller of a request.
/// </summary>
public static class AuthenticationRegistration
{
    /// <summary>
    /// Windows authentication handled by IIS. Required for in-process hosting behind IIS.
    /// </summary>
    public const string IisIntegrated = "IISIntegrated";

    /// <summary>
    /// Windows authentication performed by the application. It works on Windows without further
    /// configuration. On another system, it requires a host joined to the domain and a Kerberos
    /// keytab.
    /// </summary>
    public const string Negotiate = "Negotiate";

    /// <summary>
    /// No authentication. The server records the leases with an empty authenticated user, as it
    /// records an anonymous request.
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
        var scheme = ResolveScheme( configuration );

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
                throw new InvalidOperationException( $"Unknown authentication scheme '{scheme}'. Use '{IisIntegrated}', '{Negotiate}' or '{None}'." );
        }

        return scheme;
    }

    /// <summary>
    /// Selects the scheme when the configuration does not name one.
    /// </summary>
    /// <remarks>
    /// The same package runs behind IIS, in its own process on a Windows machine, and on a Linux host
    /// that can have no domain. A wrong selection is not reported as an error: the server records
    /// every lease with an empty authenticated user. The selection is therefore made from the way the
    /// application is hosted, and it is written to the log at startup.
    /// </remarks>
    private static string ResolveScheme( IConfiguration configuration )
    {
        var configured = configuration["Authentication:Scheme"];

        if ( !string.IsNullOrWhiteSpace( configured ) )
        {
            // The comparison ignores the case, so that "negotiate" in the environment of a container
            // is accepted.
            foreach ( var known in new[] { IisIntegrated, Negotiate, None } )
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