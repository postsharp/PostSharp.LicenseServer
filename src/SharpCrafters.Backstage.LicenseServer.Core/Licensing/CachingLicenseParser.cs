using System.Collections.Concurrent;

namespace SharpCrafters.Backstage.LicenseServer.Licensing;

/// <summary>
/// Caches the result of a parse, because the server parses a license key at every lease request and
/// at every display of the home page.
/// </summary>
/// <remarks>
/// This cache also stores the result for a key that is invalid. The legacy
/// <c>ParsedLicenseManager</c> returned before it added the entry to the dictionary, so it parsed an
/// invalid key at every call.
/// </remarks>
public sealed class CachingLicenseParser( ILicenseParser inner ) : ILicenseParser
{
    private readonly ConcurrentDictionary<string, LicenseInfo?> cache = new( StringComparer.Ordinal );

    public LicenseInfo? TryParse( string licenseKey )
        => string.IsNullOrWhiteSpace( licenseKey )
            ? null
            : this.cache.GetOrAdd( licenseKey, inner.TryParse );

    public string CleanLicenseString( string licenseKey ) => inner.CleanLicenseString( licenseKey );
}
