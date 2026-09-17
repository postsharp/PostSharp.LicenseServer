using System.Security.Cryptography;
using SharpCrafters.Backstage.Licensing;
using SharpCrafters.Backstage.Licensing.Licenses;
using SharpCrafters.Backstage.Testing;

namespace SharpCrafters.Backstage.LicenseServer.Tests.Infrastructure;

/// <summary>
/// Builds real license keys, so that the parser can be tested against the format it meets in
/// production.
/// </summary>
/// <remarks>
/// <para>
/// The keys are signed by the test licensing authority of SharpCrafters.Backstage, reached through
/// <see cref="TestLicenseKeyProvider"/>. That authority generates its key pair in the current
/// process, so a license key signed here is valid in this process and nowhere else -- which is what
/// lets a public, MIT-licensed repository test the real signature path. What this cannot cover is
/// the production authority itself, whose public keys are constants of SharpCrafters.Backstage and
/// are covered by the tests of that package.
/// </para>
/// <para>
/// <see cref="Authority"/> holds the same authority object that signs, so the tests verify against
/// exactly what signed them rather than against a reconstruction of it.
/// </para>
/// </remarks>
public static class TestLicenseKeys
{
    private static readonly TestLicenseKeyProvider provider = new();

    /// <summary>
    /// The identifier of the key of the test authority. It is a constant of SharpCrafters.Backstage
    /// but an internal one, so it is read back from a license key that the authority has signed.
    /// </summary>
    private static readonly byte authorityKeyId;

    /// <summary>
    /// Gets the authority that verifies the keys this class signs, which is what a parser under test
    /// is constructed with.
    /// </summary>
    public static ILicensingAuthorityProvider Authority { get; }

    /// <summary>
    /// Gets the ready-made license keys that SharpCrafters.Backstage issues for its own tests, one
    /// per product and license type it sells.
    /// </summary>
    public static TestLicenseKeyProvider Keys => provider;

    static TestLicenseKeys()
    {
        string probe = new LicenseKeyDataBuilder { LicenseId = 1, LicenseType = LicenseType.Business }
            .SignAndSerialize( provider.Authority );

        LicenseKeyData.TryDeserialize( probe, out LicenseKeyData? data, out _ );
        authorityKeyId = data!.SignatureKeyId!.Value;

        Authority = new TestAuthorityProvider( provider.Authority, authorityKeyId );
    }

    /// <summary>
    /// Creates a builder for a license key that the license server accepts, which the caller modifies
    /// before serializing it.
    /// </summary>
    public static LicenseKeyDataBuilder Builder(
        int licenseId = 1,
        LicenseType licenseType = LicenseType.Business,
        LicenseProduct product = LicenseProduct.PostSharpUltimate )
        => new()
        {
            LicenseId = licenseId,
            LicenseType = licenseType,
            Product = product,
            UserNumber = 5,
            LicenseServerEligible = true
        };

    /// <summary>
    /// Signs and serializes a license key with the test authority.
    /// </summary>
    public static string Sign( this LicenseKeyDataBuilder builder )
        => builder.SignAndSerialize( provider.Authority );

    /// <summary>
    /// Serializes a license key without signing it. Only the types that require no signature parse
    /// this way.
    /// </summary>
    public static string Unsigned( this LicenseKeyDataBuilder builder ) => builder.Serialize();

    /// <summary>
    /// Signs a license key with a key of the test authority's identifier that the test authority does
    /// not hold, which is a forgery: the parser looks the identifier up, finds the real key and the
    /// signature does not verify against it.
    /// </summary>
    public static string SignWithAForgedKey( this LicenseKeyDataBuilder builder )
        => builder.SignAndSerialize( CreateStandaloneAuthority( authorityKeyId ) );

    /// <summary>
    /// Signs a license key with an authority the parser has never heard of, so that the identifier of
    /// the signature matches no key it holds.
    /// </summary>
    /// <remarks>
    /// The identifier is outside the range of the production keys and of the test keys of Backstage.
    /// </remarks>
    public static string SignWithAnUnknownAuthority( this LicenseKeyDataBuilder builder )
        => builder.SignAndSerialize( CreateStandaloneAuthority( 200 ) );

    private static LicensingAuthority CreateStandaloneAuthority( byte keyId )
    {
        using ECDsa key = ECDsa.Create( ECCurve.NamedCurves.nistP256 );
        ECParameters parameters = key.ExportParameters( true );

        string xml = "<ECDSAKeyValue><Curve>nistP256</Curve>"
                     + $"<X>{Convert.ToBase64String( parameters.Q.X! )}</X>"
                     + $"<Y>{Convert.ToBase64String( parameters.Q.Y! )}</Y>"
                     + $"<D>{Convert.ToBase64String( parameters.D! )}</D>"
                     + "</ECDSAKeyValue>";

        return new ExplicitLicensingAuthorityProvider( (keyId, xml) ).GetAuthority( keyId );
    }

    /// <summary>
    /// Answers with the single authority that signed, for the identifier that authority's key carries.
    /// </summary>
    /// <remarks>
    /// <see cref="ExplicitLicensingAuthorityProvider"/> cannot be used for this, because it builds an
    /// authority from the XML representation of a key and the key of the test authority is generated
    /// in the process rather than written down.
    /// </remarks>
    private sealed class TestAuthorityProvider( LicensingAuthority authority, byte keyId ) : ILicensingAuthorityProvider
    {
        public IEnumerable<byte> KeyIds => [keyId];

        public LicensingAuthority GetAuthority( byte id )
            => id == keyId
                ? authority
                : throw new KeyNotFoundException( $"There is no test licensing authority key of identifier {id}." );
    }
}
