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
    /// The identifiers of the test licensing authorities that were added to the production
    /// authority, so that the caller can write them to the log. The list is empty on a server
    /// configured as a customer runs it.
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

        // The server refuses to start instead of ignoring the setting or applying it. Ignoring it
        // would let an administrator believe that the server accepts those license keys. Applying it
        // would let whoever holds the private key create licenses that this server serves.
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

            // A key is parsed at its first use, which would be during a lease request. Requesting
            // each authority here turns a malformed key into a failure at startup.
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
    /// Gets the directory that contains the files the server generates. A relative path is resolved
    /// against the application directory, so that the default value works wherever the release
    /// package is unpacked.
    /// </summary>
    public static string ResolveDataDirectory( IConfigurationSection section, IHostEnvironment environment )
    {
        string configured = section.GetValue( "DataDirectory", "App_Data" ) ?? "App_Data";

        return Path.IsPathRooted( configured )
            ? configured
            : Path.Combine( environment.ContentRootPath, configured );
    }
}
