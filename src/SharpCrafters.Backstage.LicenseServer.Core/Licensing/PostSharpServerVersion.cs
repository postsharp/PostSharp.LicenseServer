using PostSharp.Sdk;

namespace SharpCrafters.Backstage.LicenseServer.Licensing;

/// <summary>
/// Reports the version of the PostSharp SDK embedded in this license server.
/// </summary>
public sealed class PostSharpServerVersion : ILicenseServerVersion
{
    public Version SdkVersion => ApplicationInfo.Version;
}
