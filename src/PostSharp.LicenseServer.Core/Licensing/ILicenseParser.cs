namespace PostSharp.LicenseServer.Licensing;

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
    /// Removes whitespace and formatting from a license key pasted by a human.
    /// </summary>
    string CleanLicenseString( string licenseKey );
}
