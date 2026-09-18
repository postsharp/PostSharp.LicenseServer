using SharpCrafters.Backstage.LicenseServer.Licensing;

namespace SharpCrafters.Backstage.LicenseServer.Tests.Fakes;

/// <summary>
/// Reports a fixed version of the licensing library, so that a test can reach the branch in which
/// the license server is older than the license key requires.
/// </summary>
public sealed class FixedServerVersion : ILicenseServerVersion
{
    public FixedServerVersion() : this( new Version( 2025, 1, 5 ) ) { }

    public FixedServerVersion( Version version )
    {
        this.LicensingLibraryVersion = version;
    }

    public Version LicensingLibraryVersion { get; }
}
