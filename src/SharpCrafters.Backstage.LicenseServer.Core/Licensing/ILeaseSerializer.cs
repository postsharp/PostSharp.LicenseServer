namespace SharpCrafters.Backstage.LicenseServer.Licensing;

/// <summary>
/// Produces the response body of the lease endpoint. This is the wire contract with the PostSharp
/// client, so it is isolated behind an interface and pinned by tests.
/// </summary>
public interface ILeaseSerializer
{
    string Serialize( string licenseKey, DateTime startTime, DateTime endTime, DateTime renewTime );
}
