// Copyright (c) SharpCrafters s.r.o. See the LICENSE.md file in the root directory of this repository root for details.

using SharpCrafters.Backstage.LicenseServer.Licensing;

namespace SharpCrafters.Backstage.LicenseServer.Tests.Fakes;

/// <summary>
/// Maps the synthetic license keys of a test to the properties that the test gives them.
/// </summary>
/// <remarks>
/// A real PostSharp license key is signed, and this repository contains none, so almost every test
/// uses this parser. <c>BackstageLicenseParserTests</c> covers the real parser with the license keys
/// of the test authority.
/// </remarks>
public sealed class FakeLicenseParser : ILicenseParser
{
    private readonly Dictionary<string, LicenseInfo> licenses = new( StringComparer.Ordinal );

    /// <summary>
    /// Gets the number of calls to <see cref="TryParse"/> that did the work, so that a test can
    /// verify the cache.
    /// </summary>
    public int ParseCount { get; private set; }

    public void Register( string licenseKey, LicenseInfo info ) => this.licenses[licenseKey] = info;

    public LicenseInfo? TryParse( string licenseKey )
    {
        this.ParseCount++;

        return this.licenses.GetValueOrDefault( licenseKey );
    }

    /// <summary>
    /// Reproduces the real implementation, which removes the whitespace of a pasted key.
    /// </summary>
    public string CleanLicenseString( string licenseKey ) => new( licenseKey.Where( c => !char.IsWhiteSpace( c ) ).ToArray() );
}