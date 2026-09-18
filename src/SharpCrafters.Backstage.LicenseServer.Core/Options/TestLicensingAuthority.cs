namespace SharpCrafters.Backstage.LicenseServer.Options;

/// <summary>
/// One licensing authority whose license keys a development server accepts, in addition to the
/// production authority.
/// </summary>
/// <remarks>
/// This setting contains only the public half of the key pair. The public half verifies a signature
/// and cannot create one. Whoever holds the private half can create license keys that a server
/// configured in this way accepts, so the server refuses this setting outside the Development
/// environment.
/// </remarks>
public sealed class TestLicensingAuthority
{
    /// <summary>
    /// Gets or sets the identifier that the signature of a license key contains. It must differ from
    /// the identifiers of the production keys, which are 0, 1 and 2.
    /// </summary>
    public byte KeyId { get; set; }

    /// <summary>
    /// Gets or sets the public key, in the XML representation that SharpCrafters.Backstage reads:
    /// an <c>ECDSAKeyValue</c> element for an Elliptic Curve DSA key, or a <c>DSAKeyValue</c> element
    /// for a finite field DSA one.
    /// </summary>
    public string PublicKey { get; set; } = "";
}
