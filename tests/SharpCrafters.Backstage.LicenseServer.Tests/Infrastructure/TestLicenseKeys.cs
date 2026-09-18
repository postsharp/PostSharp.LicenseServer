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
/// The test licensing authority of SharpCrafters.Backstage signs the keys, and
/// <see cref="TestLicenseKeyProvider"/> gives access to it. That authority generates its key pair in
/// the current process, so a license key signed here is valid in this process and in no other one.
/// A public repository under the MIT license can therefore test the real code path of the signature.
/// These tests do not cover the production authority, whose public keys are constants of
/// SharpCrafters.Backstage, and which the tests of that package cover.
/// </para>
/// <para>
/// <see cref="Authority"/> holds the authority object that signs the keys, so a test verifies a key
/// against the authority that signed it, and not against another instance of that authority.
/// </para>
/// </remarks>
public static class TestLicenseKeys
{
    private static readonly TestLicenseKeyProvider provider = new();

    /// <summary>
    /// The identifier of the key of the test authority. It is a constant of SharpCrafters.Backstage,
    /// and that constant is internal, so this class reads the identifier from a license key that the
    /// authority signed.
    /// </summary>
    private static readonly byte authorityKeyId;

    /// <summary>
    /// Gets the authority that verifies the keys this class signs. A test passes it to the parser
    /// that it exercises.
    /// </summary>
    public static ILicensingAuthorityProvider Authority { get; }

    /// <summary>
    /// Gets the license keys that SharpCrafters.Backstage creates for its own tests. There is one
    /// key per product and per type of license.
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
    /// Creates a builder of a license key that the license server accepts. The caller modifies the
    /// builder before it serializes the key.
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
    /// Serializes a license key without signing it. Only the types of license that require no
    /// signature can be parsed in this form.
    /// </summary>
    public static string Unsigned( this LicenseKeyDataBuilder builder ) => builder.Serialize();

    /// <summary>
    /// Signs a license key with another key that carries the identifier of the test authority. The
    /// result is a forgery: the parser reads the identifier, finds the real key, and the signature
    /// does not verify against it.
    /// </summary>
    public static string SignWithAForgedKey( this LicenseKeyDataBuilder builder )
        => builder.SignAndSerialize( CreateStandaloneAuthority( authorityKeyId ) );

    /// <summary>
    /// Signs a license key with an authority that the parser does not know, so that the identifier of
    /// the signature matches no key the parser holds.
    /// </summary>
    /// <remarks>
    /// The identifier differs from the identifiers of the production keys and from the identifiers of
    /// the test keys of Backstage.
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
    /// Returns the authority that signed the keys, for the identifier that the key of that authority
    /// carries.
    /// </summary>
    /// <remarks>
    /// <see cref="ExplicitLicensingAuthorityProvider"/> cannot do this, because it builds an
    /// authority from the XML representation of a key, and the key of the test authority is generated
    /// in the process instead of being written in the code.
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
