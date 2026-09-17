namespace SharpCrafters.Backstage.LicenseServer.Licensing;

/// <summary>
/// The version of the PostSharp SDK embedded in this license server. A license requiring a higher
/// version cannot be served until the license server itself is upgraded.
/// </summary>
public interface ILicenseServerVersion
{
    Version SdkVersion { get; }
}
