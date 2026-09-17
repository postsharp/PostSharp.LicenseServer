namespace SharpCrafters.Backstage.LicenseServer.Licensing;

/// <summary>
/// The version of the licensing library embedded in this license server. A license key that declares
/// a higher minimal version cannot be served until the server itself is upgraded.
/// </summary>
public interface ILicenseServerVersion
{
    Version LicensingLibraryVersion { get; }
}
