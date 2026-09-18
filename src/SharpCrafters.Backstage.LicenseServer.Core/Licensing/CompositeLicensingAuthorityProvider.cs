using SharpCrafters.Backstage.Licensing.Licenses;

namespace SharpCrafters.Backstage.LicenseServer.Licensing;

/// <summary>
/// Accepts the signature of a license key issued by any one of several licensing authorities.
/// </summary>
/// <remarks>
/// The identifiers of the keys must be distinct across the providers, because the identifier that a
/// signature carries selects the key that verifies it. The constructor rejects a duplicate
/// identifier instead of selecting the first provider, because the first provider depends on the
/// order in which the providers were passed.
/// </remarks>
public sealed class CompositeLicensingAuthorityProvider : ILicensingAuthorityProvider
{
    private readonly Dictionary<byte, ILicensingAuthorityProvider> providers = [];

    public CompositeLicensingAuthorityProvider( params ILicensingAuthorityProvider[] providers )
    {
        ArgumentNullException.ThrowIfNull( providers );

        foreach ( ILicensingAuthorityProvider provider in providers )
        {
            foreach ( byte keyId in provider.KeyIds )
            {
                if ( !this.providers.TryAdd( keyId, provider ) )
                {
                    throw new ArgumentException(
                        $"Two licensing authorities declare the key of identifier {keyId}.",
                        nameof(providers) );
                }
            }
        }
    }

    public IEnumerable<byte> KeyIds => this.providers.Keys;

    public LicensingAuthority GetAuthority( byte keyId )
        => this.providers.TryGetValue( keyId, out ILicensingAuthorityProvider? provider )
            ? provider.GetAuthority( keyId )
            : throw new KeyNotFoundException( $"There is no licensing authority key of identifier {keyId}." );
}
