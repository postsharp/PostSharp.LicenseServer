using SharpCrafters.Backstage.Licensing.Licenses;

namespace SharpCrafters.Backstage.LicenseServer.Licensing;

/// <summary>
/// Accepts the signature of a license key issued by any one of several licensing authorities.
/// </summary>
/// <remarks>
/// The identifiers of the keys have to be distinct across the providers, because the identifier
/// carried by a signature is what selects the key that verifies it. The constructor rejects a
/// duplicate rather than letting the first provider win, since which one that is would depend on the
/// order the providers were passed in.
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
