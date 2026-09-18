using System.Security.Cryptography;
using Microsoft.Extensions.Logging;
using SharpCrafters.Backstage.Licensing;
using SharpCrafters.Backstage.Licensing.Licenses;

namespace SharpCrafters.Backstage.LicenseServer.Licensing;

/// <summary>
/// A licensing authority that a development server uses to issue to itself the license keys it
/// serves.
/// </summary>
/// <remarks>
/// <para>
/// A server that has no license key cannot serve a lease, and the production authority signs only
/// license keys that were sold. This authority exists for an evaluation and for a load simulation.
/// It signs license keys that only a server holding the same key pair accepts.
/// </para>
/// <para>
/// The key pair is generated at the first use and stored in the data directory, next to the audit
/// signing key. It must survive the process, because the license keys it signed are stored in the
/// database and stop being valid when the pair changes. In a container, the data directory must
/// therefore be a volume.
/// </para>
/// </remarks>
public sealed class TestLicenseAuthority
{
    /// <summary>
    /// The identifier that the signature of these license keys contains. It differs from the
    /// identifiers of the production keys, which are 0, 1 and 2.
    /// </summary>
    public const byte KeyId = 200;

    private readonly LicensingAuthority signingAuthority;

    /// <summary>
    /// Gets the provider that verifies the license keys this authority signs. It holds the public
    /// half of the key pair, and the server passes it to the license parser.
    /// </summary>
    public ILicensingAuthorityProvider Authority { get; }

    private TestLicenseAuthority( ECParameters parameters )
    {
        this.signingAuthority = CreateAuthority( ToXml( parameters, true ) );
        this.Authority = new ExplicitLicensingAuthorityProvider( (KeyId, ToXml( parameters, false )) );
    }

    /// <summary>
    /// Reads the key pair from <paramref name="keyFilePath"/>. When the file does not exist, it
    /// generates a key pair and stores it in that file.
    /// </summary>
    public static TestLicenseAuthority LoadOrCreate( string keyFilePath, ILogger logger )
    {
        ArgumentException.ThrowIfNullOrWhiteSpace( keyFilePath );
        ArgumentNullException.ThrowIfNull( logger );

        using ECDsa key = ECDsa.Create( ECCurve.NamedCurves.nistP256 );

        if ( File.Exists( keyFilePath ) )
        {
            key.ImportECPrivateKey( Convert.FromBase64String( File.ReadAllText( keyFilePath ).Trim() ), out _ );

            return new TestLicenseAuthority( key.ExportParameters( true ) );
        }

        Directory.CreateDirectory( Path.GetDirectoryName( keyFilePath )! );
        File.WriteAllText( keyFilePath, Convert.ToBase64String( key.ExportECPrivateKey() ) );

        logger.LogInformation(
            "Generated a test licensing authority in {Path}. The license keys it signs are accepted by "
            + "this server alone, and stop being accepted if the file is lost.",
            keyFilePath );

        return new TestLicenseAuthority( key.ExportParameters( true ) );
    }

    /// <summary>
    /// Signs a license key that this server accepts.
    /// </summary>
    public string CreateLicenseKey(
        int licenseId,
        LicenseProduct product,
        LicenseType licenseType,
        short userNumber,
        byte graceDays,
        byte gracePercent,
        DateTime validTo )
    {
        LicenseKeyDataBuilder builder = new()
        {
            LicenseId = licenseId,
            Product = product,
            LicenseType = licenseType,
            UserNumber = userNumber,
            GraceDays = graceDays,
            GracePercent = gracePercent,
            LicenseServerEligible = true,
            ValidTo = validTo,
            SubscriptionEndDate = validTo
        };

        return builder.SignAndSerialize( this.signingAuthority );
    }

    private static LicensingAuthority CreateAuthority( string keyXml )
        => new ExplicitLicensingAuthorityProvider( (KeyId, keyXml) ).GetAuthority( KeyId );

    private static string ToXml( ECParameters parameters, bool includePrivateValue )
        => "<ECDSAKeyValue><Curve>nistP256</Curve>"
           + $"<X>{Convert.ToBase64String( parameters.Q.X! )}</X>"
           + $"<Y>{Convert.ToBase64String( parameters.Q.Y! )}</Y>"
           + (includePrivateValue ? $"<D>{Convert.ToBase64String( parameters.D! )}</D>" : string.Empty)
           + "</ECDSAKeyValue>";
}
