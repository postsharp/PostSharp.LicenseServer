using System.Globalization;
using System.Security.Cryptography;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using SharpCrafters.Backstage.LicenseServer.Licensing;
using SharpCrafters.Backstage.LicenseServer.Tests.Infrastructure;
using SharpCrafters.Backstage.Licensing.Licenses;

namespace SharpCrafters.Backstage.LicenseServer.Tests;

/// <summary>
/// The licensing authorities whose license keys a development server accepts, in addition to the
/// production authority. They let a load simulation run against license keys that are not sold.
/// </summary>
public sealed class TestLicensingAuthorityTests
{
    private const byte keyId = 200;

    /// <summary>
    /// A license key signed by a configured authority is parsed, and the production authority still
    /// works alongside it.
    /// </summary>
    [Fact]
    public void ConfiguredAuthority_InDevelopment_IsAccepted()
    {
        (string publicKey, string licenseKey) = CreateAuthorityAndKey();

        ILicenseParser parser = BuildParser( "Development", (keyId, publicKey) );

        Assert.NotNull( parser.TryParse( licenseKey ) );
    }

    /// <summary>
    /// A license key signed by an authority that is not configured is rejected, so the setting widens
    /// what the server accepts by exactly the keys it names.
    /// </summary>
    [Fact]
    public void UnconfiguredAuthority_InDevelopment_IsRejected()
    {
        (string publicKey, _) = CreateAuthorityAndKey();
        (_, string otherLicenseKey) = CreateAuthorityAndKey();

        ILicenseParser parser = BuildParser( "Development", (keyId, publicKey) );

        Assert.Null( parser.TryParse( otherLicenseKey ) );
    }

    /// <summary>
    /// Outside the Development environment, the server refuses to start instead of ignoring the
    /// setting. The administrator who set it is informed, and no server accepts the license keys of
    /// whoever holds the private key.
    /// </summary>
    [Theory]
    [InlineData( "Production" )]
    [InlineData( "Staging" )]
    [InlineData( "Testing" )]
    public void ConfiguredAuthority_OutsideDevelopment_RefusesToStart( string environmentName )
    {
        (string publicKey, _) = CreateAuthorityAndKey();

        InvalidOperationException exception = Assert.Throws<InvalidOperationException>(
            () => BuildParser( environmentName, (keyId, publicKey) ) );

        Assert.Contains( "TestLicensingAuthorities", exception.Message, StringComparison.Ordinal );
        Assert.Contains( environmentName, exception.Message, StringComparison.Ordinal );
    }

    /// <summary>
    /// A server that configures none of these starts as it always did, and accepts the production
    /// authority alone.
    /// </summary>
    [Fact]
    public void NoConfiguredAuthority_OutsideDevelopment_Starts()
    {
        (_, string licenseKey) = CreateAuthorityAndKey();

        ILicenseParser parser = BuildParser( "Production" );

        Assert.Null( parser.TryParse( licenseKey ) );
    }

    /// <summary>
    /// A key whose identifier is one of the production ones is refused, because the identifier is what
    /// selects the key that verifies a signature and the production authority must keep its own.
    /// </summary>
    [Fact]
    public void ConfiguredAuthority_TakingAProductionKeyIdentifier_IsRefused()
    {
        (string publicKey, _) = CreateAuthorityAndKey();

        Assert.Throws<ArgumentException>( () => BuildParser( "Development", (2, publicKey) ) );
    }

    /// <summary>
    /// A malformed key fails at startup rather than in the middle of a lease request, which is when
    /// the authority would otherwise first be built.
    /// </summary>
    [Fact]
    public void ConfiguredAuthority_WithAMalformedKey_FailsAtStartup()
        => Assert.ThrowsAny<Exception>( () => BuildParser( "Development", (keyId, "not-a-key") ) );

    private static ILicenseParser BuildParser( string environmentName, params (int KeyId, string PublicKey)[] authorities )
    {
        Dictionary<string, string?> settings = [];

        for ( int i = 0; i < authorities.Length; i++ )
        {
            settings[$"LicenseServer:TestLicensingAuthorities:{i}:KeyId"] = authorities[i].KeyId.ToString( CultureInfo.InvariantCulture );
            settings[$"LicenseServer:TestLicensingAuthorities:{i}:PublicKey"] = authorities[i].PublicKey;
        }

        IConfiguration configuration = new ConfigurationBuilder().AddInMemoryCollection( settings ).Build();

        ServiceCollection services = [];
        services.AddLicenseServerLicensing( configuration, new StubEnvironment( environmentName ) );

        return services.BuildServiceProvider().GetRequiredService<ILicenseParser>();
    }

    /// <summary>
    /// Creates a licensing authority and a license key that it signs.
    /// </summary>
    private static (string PublicKey, string LicenseKey) CreateAuthorityAndKey()
    {
        using ECDsa key = ECDsa.Create( ECCurve.NamedCurves.nistP256 );
        ECParameters parameters = key.ExportParameters( true );

        string ToXml( bool includePrivateValue )
            => "<ECDSAKeyValue><Curve>nistP256</Curve>"
               + $"<X>{Convert.ToBase64String( parameters.Q.X! )}</X>"
               + $"<Y>{Convert.ToBase64String( parameters.Q.Y! )}</Y>"
               + (includePrivateValue ? $"<D>{Convert.ToBase64String( parameters.D! )}</D>" : "")
               + "</ECDSAKeyValue>";

        LicensingAuthority authority =
            new ExplicitLicensingAuthorityProvider( (keyId, ToXml( true )) ).GetAuthority( keyId );

        string licenseKey = TestLicenseKeys.Builder().SignAndSerialize( authority );

        return (ToXml( false ), licenseKey);
    }

    private sealed class StubEnvironment : IHostEnvironment
    {
        public StubEnvironment( string environmentName )
        {
            this.EnvironmentName = environmentName;
        }

        public string EnvironmentName { get; set; }

        public string ApplicationName { get; set; } = "Tests";

        public string ContentRootPath { get; set; } = AppContext.BaseDirectory;

        public Microsoft.Extensions.FileProviders.IFileProvider ContentRootFileProvider { get; set; } =
            new Microsoft.Extensions.FileProviders.NullFileProvider();
    }
}
