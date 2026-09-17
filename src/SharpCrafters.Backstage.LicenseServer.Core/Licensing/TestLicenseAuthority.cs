using System.Security.Cryptography;
using Microsoft.Extensions.Logging;
using SharpCrafters.Backstage.Licensing;
using SharpCrafters.Backstage.Licensing.Licenses;

namespace SharpCrafters.Backstage.LicenseServer.Licensing;

/// <summary>
/// A licensing authority of its own, which a development server uses to issue the license keys it
/// serves to itself.
/// </summary>
/// <remarks>
/// <para>
/// A server that has no license key cannot serve a lease, and every key the production authority
/// signs is one that was sold. This authority fills that gap for a trial and for a load simulation:
/// it signs keys that only a server holding the same key pair accepts.
/// </para>
/// <para>
/// The key pair is generated on first use and kept in the data directory, next to the audit signing
/// key. It has to outlive the process, because the license keys it signed are in the database and
/// stop verifying when the pair changes. In a container that means the data directory has to be a
/// volume.
/// </para>
/// </remarks>
public sealed class TestLicenseAuthority
{
    /// <summary>
    /// The identifier the signature of these license keys carries. It is outside the range of the
    /// production keys, which are 0, 1 and 2.
    /// </summary>
    public const byte KeyId = 200;

    private readonly LicensingAuthority signingAuthority;

    /// <summary>
    /// Gets the provider that verifies the license keys this authority signs, holding the public half
    /// of the pair. It is what the license parser of the server is given.
    /// </summary>
    public ILicensingAuthorityProvider Authority { get; }

    private TestLicenseAuthority( ECParameters parameters )
    {
        this.signingAuthority = CreateAuthority( ToXml( parameters, true ) );
        this.Authority = new ExplicitLicensingAuthorityProvider( (KeyId, ToXml( parameters, false )) );
    }

    /// <summary>
    /// Reads the key pair from <paramref name="keyFilePath"/>, generating and storing one when the
    /// file is absent.
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
