using PostSharp.Sdk.Extensibility.Licensing;

namespace PostSharp.LicenseServer.Licensing;

/// <summary>
/// Serializes a lease in the format expected by the PostSharp client.
/// </summary>
public sealed class PostSharpLeaseSerializer : ILeaseSerializer
{
    public string Serialize( string licenseKey, DateTime startTime, DateTime endTime, DateTime renewTime )
        => new LicenseLease( licenseKey, startTime, endTime, renewTime ).Serialize();
}
