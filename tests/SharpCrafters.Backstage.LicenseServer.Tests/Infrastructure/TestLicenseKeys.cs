using System.Security.Cryptography;
using SharpCrafters.Backstage.Licensing;
using SharpCrafters.Backstage.Licensing.Licenses;

namespace SharpCrafters.Backstage.LicenseServer.Tests.Infrastructure;

/// <summary>
/// Builds real license keys, so that the parser can be tested against the format it meets in
/// production.
/// </summary>
/// <remarks>
/// <para>
/// No license key signed by the production authority is present in this repository, and none can be:
/// it is public and MIT-licensed. The keys are therefore signed with a key pair generated here, and
/// the parser under test is given the public half of that pair. What this cannot cover is the
/// production authority itself, whose public keys are constants of SharpCrafters.Backstage and are
/// covered by the tests of that package.
/// </para>
/// <para>
/// The key pair is Elliptic Curve DSA on <c>nistP256</c>, which is the algorithm of every license key
/// issued since 2026 and the only one available on every platform.
/// </para>
/// </remarks>
public static class TestLicenseKeys
{
    /// <summary>
    /// An identifier outside the range of the production keys and of the test keys of Backstage, so
    /// that a key signed here can never be mistaken for one of theirs.
    /// </summary>
    private const byte authorityKeyId = 200;

    private static readonly ILicensingAuthorityProvider signingAuthority;

    /// <summary>
    /// Gets the authority that verifies the keys this class signs, holding the public key only. A
    /// parser under test is constructed with it.
    /// </summary>
    public static ILicensingAuthorityProvider Authority { get; }

    static TestLicenseKeys()
    {
        using ECDsa key = ECDsa.Create( ECCurve.NamedCurves.nistP256 );
        ECParameters parameters = key.ExportParameters( true );

        signingAuthority = new ExplicitLicensingAuthorityProvider( (authorityKeyId, ToXml( parameters, true )) );
        Authority = new ExplicitLicensingAuthorityProvider( (authorityKeyId, ToXml( parameters, false )) );
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
        => builder.SignAndSerialize( signingAuthority.GetAuthority( authorityKeyId ) );

    /// <summary>
    /// Serializes a license key without signing it. Only the types that require no signature parse
    /// this way.
    /// </summary>
    public static string Unsigned( this LicenseKeyDataBuilder builder ) => builder.Serialize();

    /// <summary>
    /// Signs a license key with a second key pair, which no parser under test is given the authority
    /// of.
    /// </summary>
    public static string SignWithAnotherAuthority( this LicenseKeyDataBuilder builder )
    {
        using ECDsa other = ECDsa.Create( ECCurve.NamedCurves.nistP256 );

        var provider = new ExplicitLicensingAuthorityProvider(
            (authorityKeyId, ToXml( other.ExportParameters( true ), true )) );

        return builder.SignAndSerialize( provider.GetAuthority( authorityKeyId ) );
    }

    private static string ToXml( ECParameters parameters, bool includePrivateValue )
        => "<ECDSAKeyValue><Curve>nistP256</Curve>"
           + $"<X>{Convert.ToBase64String( parameters.Q.X! )}</X>"
           + $"<Y>{Convert.ToBase64String( parameters.Q.Y! )}</Y>"
           + (includePrivateValue ? $"<D>{Convert.ToBase64String( parameters.D! )}</D>" : string.Empty)
           + "</ECDSAKeyValue>";
}
