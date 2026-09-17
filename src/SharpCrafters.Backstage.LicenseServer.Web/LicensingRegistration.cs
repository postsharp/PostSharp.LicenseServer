using Microsoft.Extensions.Logging.Abstractions;
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
    /// Registers the license key parser, and the server's own licensing authority when it issues
    /// itself test license keys.
    /// </summary>
    /// <returns>
    /// The identifiers of the test licensing authorities that were added to the production one, for
    /// logging. Empty on a server configured the way a customer runs it.
    /// </returns>
    /// <exception cref="InvalidOperationException">
    /// A test licensing authority is configured, or test license keys are asked for, outside the
    /// Development environment.
    /// </exception>
    public static IReadOnlyList<byte> AddLicenseServerLicensing(
        this IServiceCollection services,
        IConfiguration configuration,
        IHostEnvironment environment )
    {
        IConfigurationSection section = configuration.GetSection( LicenseServerOptions.SectionName );

        TestLicensingAuthority[] testAuthorities =
            section.GetSection( "TestLicensingAuthorities" ).Get<TestLicensingAuthority[]>() ?? [];

        bool seedTestLicenses = section.GetValue( "SeedTestLicenses", false );

        // Refusing to start is deliberate. Ignoring either setting would leave an administrator
        // believing the server accepts those license keys, and honouring it would let whoever holds
        // the private key mint licenses this server serves.
        if ( !environment.IsDevelopment() )
        {
            Refuse( testAuthorities.Length > 0, "TestLicensingAuthorities is set" );
            Refuse( seedTestLicenses, "SeedTestLicenses is on" );
        }

        ILicensingAuthorityProvider authorities = new ProductionLicensingAuthorityProvider();
        List<byte> testKeyIds = [];

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
            testKeyIds.AddRange( testAuthorities.Select( a => a.KeyId ) );
        }

        if ( seedTestLicenses )
        {
            TestLicenseAuthority ownAuthority = TestLicenseAuthority.LoadOrCreate(
                Path.Combine( ResolveDataDirectory( section, environment ), "test-authority.key" ),
                NullLogger.Instance );

            services.AddSingleton( ownAuthority );

            authorities = new CompositeLicensingAuthorityProvider( authorities, ownAuthority.Authority );
            testKeyIds.Add( TestLicenseAuthority.KeyId );
        }

        services.AddSingleton<ILicenseParser>(
            _ => new CachingLicenseParser( new BackstageLicenseParser( authorities ) ) );

        return testKeyIds;

        void Refuse( bool condition, string what )
        {
            if ( condition )
            {
                throw new InvalidOperationException(
                    $"{LicenseServerOptions.SectionName}:{what}, but the environment is "
                    + $"'{environment.EnvironmentName}' and not 'Development'. A server that is not a development "
                    + "server serves license keys of the production licensing authority only. Remove the setting." );
            }
        }
    }

    /// <summary>
    /// Gets the directory holding the files the server generates and must not lose. A relative path
    /// is resolved against the application, so that the default works wherever it is unpacked.
    /// </summary>
    public static string ResolveDataDirectory( IConfigurationSection section, IHostEnvironment environment )
    {
        string configured = section.GetValue( "DataDirectory", "App_Data" ) ?? "App_Data";

        return Path.IsPathRooted( configured )
            ? configured
            : Path.Combine( environment.ContentRootPath, configured );
    }
}
