using SharpCrafters.Backstage.LicenseServer.Licensing;
using SharpCrafters.Backstage.LicenseServer.Options;
using SharpCrafters.Backstage.Licensing.Licenses;

namespace SharpCrafters.Backstage.LicenseServer;

/// <summary>
/// Registers the component that parses and validates license keys, and decides which licensing
/// authorities it accepts them from.
/// </summary>
public static class LicensingRegistration
{
    /// <summary>
    /// Registers the license key parser.
    /// </summary>
    /// <returns>
    /// The identifiers of the test licensing authorities that were added to the production one, for
    /// logging. Empty on a server configured the way a customer runs it.
    /// </returns>
    /// <exception cref="InvalidOperationException">
    /// A test licensing authority is configured outside the Development environment.
    /// </exception>
    public static IReadOnlyList<byte> AddLicenseServerLicensing(
        this IServiceCollection services,
        IConfiguration configuration,
        IHostEnvironment environment )
    {
        TestLicensingAuthority[] testAuthorities = configuration
            .GetSection( $"{LicenseServerOptions.SectionName}:TestLicensingAuthorities" )
            .Get<TestLicensingAuthority[]>() ?? [];

        if ( testAuthorities.Length > 0 && !environment.IsDevelopment() )
        {
            // Refusing to start is deliberate. Ignoring the setting would leave an administrator
            // believing the server accepts those license keys, and accepting it would let whoever
            // holds the private key mint licenses this server honours.
            throw new InvalidOperationException(
                $"{LicenseServerOptions.SectionName}:TestLicensingAuthorities is set, but the environment is "
                + $"'{environment.EnvironmentName}' and not 'Development'. A server that is not a development "
                + "server accepts license keys from the production licensing authority only. Remove the setting." );
        }

        ILicensingAuthorityProvider authorities = new ProductionLicensingAuthorityProvider();

        if ( testAuthorities.Length > 0 )
        {
            var explicitAuthorities = new ExplicitLicensingAuthorityProvider(
                testAuthorities.Select( a => ((int) a.KeyId, a.PublicKey) ).ToArray() );

            // The key is parsed when it is first used, which would otherwise be in the middle of a
            // lease request. Asking for each authority here turns a malformed key into a startup
            // failure.
            foreach ( TestLicensingAuthority authority in testAuthorities )
            {
                explicitAuthorities.GetAuthority( authority.KeyId );
            }

            authorities = new CompositeLicensingAuthorityProvider( authorities, explicitAuthorities );
        }

        services.AddSingleton<ILicenseParser>(
            _ => new CachingLicenseParser( new BackstageLicenseParser( authorities ) ) );

        return testAuthorities.Select( a => a.KeyId ).ToArray();
    }
}
