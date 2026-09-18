using SharpCrafters.Backstage.Licensing.Licenses;

namespace SharpCrafters.Backstage.LicenseServer.Licensing;

/// <summary>
/// Reports the version of SharpCrafters.Backstage, which is the library that parses license keys in
/// this server.
/// </summary>
/// <remarks>
/// The version is read from the assembly and not written in the code, so that an upgrade of the
/// package is sufficient to serve a license key that requires a newer version.
/// </remarks>
public sealed class BackstageServerVersion : ILicenseServerVersion
{
    public Version LicensingLibraryVersion { get; } =
        typeof(LicenseKeyData).Assembly.GetName().Version ?? new Version( 0, 0 );
}
