using System.Collections.Concurrent;

namespace PostSharp.LicenseServer.Licensing;

/// <summary>
/// Caches parse results, because a license key is parsed on every lease request and on every render
/// of the dashboard.
/// </summary>
/// <remarks>
/// Unlike the legacy <c>ParsedLicenseManager</c>, this cache also remembers that a key is
/// <i>invalid</i>. The old code returned before adding to the dictionary, so an invalid key was
/// re-parsed on every single call.
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
