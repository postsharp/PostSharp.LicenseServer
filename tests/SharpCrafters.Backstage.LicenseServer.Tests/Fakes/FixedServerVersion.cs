using SharpCrafters.Backstage.LicenseServer.Licensing;

namespace SharpCrafters.Backstage.LicenseServer.Tests.Fakes;

/// <summary>
/// Reports a fixed version of the licensing library, so that the "the license server itself is too
/// old" branch can be reached from a test.
/// </summary>
public sealed class FixedServerVersion( Version version ) : ILicenseServerVersion
{
    public FixedServerVersion() : this( new Version( 2025, 1, 5 ) ) { }

    public Version LicensingLibraryVersion { get; } = version;
}
