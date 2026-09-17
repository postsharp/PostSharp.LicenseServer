using SharpCrafters.Backstage.Licensing.Licenses;

namespace SharpCrafters.Backstage.LicenseServer.Licensing;

/// <summary>
/// Reports the version of SharpCrafters.Backstage, which is the library that parses license keys in
/// this server.
/// </summary>
/// <remarks>
/// The version is read from the assembly rather than written down, so that upgrading the package is
/// all that is needed to serve a license key that requires a newer one.
/// </remarks>
public sealed class BackstageServerVersion : ILicenseServerVersion
{
    public Version LicensingLibraryVersion { get; } =
        typeof(LicenseKeyData).Assembly.GetName().Version ?? new Version( 0, 0 );
}
