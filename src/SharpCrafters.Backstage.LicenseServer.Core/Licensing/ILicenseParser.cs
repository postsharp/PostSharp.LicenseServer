// Copyright (c) SharpCrafters s.r.o. See the LICENSE.md file in the root directory of this repository root for details.

namespace SharpCrafters.Backstage.LicenseServer.Licensing;

/// <summary>
/// Parses and validates PostSharp license keys. Replaces the static <c>ParsedLicenseManager</c>.
/// </summary>
public interface ILicenseParser
{
    /// <summary>
    /// Parses and validates a license key.
    /// </summary>
    /// <returns>The parsed license, or <c>null</c> when the key is malformed or invalid.</returns>
    LicenseInfo? TryParse( string licenseKey );

    /// <summary>
    /// Removes the whitespace and the formatting from a license key that a person pasted into a
    /// form.
    /// </summary>
    string CleanLicenseString( string licenseKey );
}