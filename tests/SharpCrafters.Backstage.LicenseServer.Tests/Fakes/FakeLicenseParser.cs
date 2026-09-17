using PostSharp.LicenseServer.Licensing;

namespace PostSharp.LicenseServer.Tests.Fakes;

/// <summary>
/// Resolves synthetic license keys to the facts a test wants them to carry.
/// </summary>
/// <remarks>
/// Real PostSharp license keys are signed and are not present in this repository, so almost every
/// test works against this parser. <c>PostSharpLicenseParser</c> is covered separately, by an opt-in
/// test that needs a real key.
/// </remarks>
public sealed class FakeLicenseParser : ILicenseParser
{
    private readonly Dictionary<string, LicenseInfo> licenses = new( StringComparer.Ordinal );

    /// <summary>
    /// Gets the number of times <see cref="TryParse"/> actually did work, to verify caching.
    /// </summary>
    public int ParseCount { get; private set; }

    public void Register( string licenseKey, LicenseInfo info ) => this.licenses[licenseKey] = info;

    public LicenseInfo? TryParse( string licenseKey )
    {
        this.ParseCount++;

        return this.licenses.GetValueOrDefault( licenseKey );
    }

    /// <summary>
    /// Mimics the real implementation, which strips whitespace from a pasted key.
    /// </summary>
    public string CleanLicenseString( string licenseKey )
        => new( licenseKey.Where( c => !char.IsWhiteSpace( c ) ).ToArray() );
}
