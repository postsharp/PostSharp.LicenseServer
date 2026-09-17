namespace SharpCrafters.Backstage.LicenseServer.Options;

/// <summary>
/// One licensing authority that a development server accepts license keys from, besides the
/// production one.
/// </summary>
/// <remarks>
/// Only the public half of the key pair goes here: it verifies a signature and cannot create one.
/// Whoever holds the private half can nevertheless mint license keys that a server configured this
/// way accepts, which is why the setting is refused outside the Development environment.
/// </remarks>
public sealed class TestLicensingAuthority
{
    /// <summary>
    /// Gets or sets the identifier the signature of a license key carries. It has to differ from the
    /// identifiers of the production keys, which are 0, 1 and 2.
    /// </summary>
    public byte KeyId { get; set; }

    /// <summary>
    /// Gets or sets the public key, in the XML representation that SharpCrafters.Backstage reads:
    /// an <c>ECDSAKeyValue</c> element for an Elliptic Curve DSA key, or a <c>DSAKeyValue</c> element
    /// for a finite field DSA one.
    /// </summary>
    public string PublicKey { get; set; } = "";
}
