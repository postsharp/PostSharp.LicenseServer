namespace SharpCrafters.Backstage.LicenseServer.Licensing;

/// <summary>
/// Produces the body of the response of the lease endpoint. This format is the contract with the
/// PostSharp client, so an interface isolates it and the tests verify it.
/// </summary>
public interface ILeaseSerializer
{
    string Serialize( string licenseKey, DateTime startTime, DateTime endTime, DateTime renewTime );
}
