namespace SharpCrafters.Backstage.LicenseServer.Security;

/// <summary>
/// Signs the audit log of the leases. The signature of a lease covers the signature of the previous
/// lease, so that a removed row and a modified row both break the chain.
/// </summary>
public interface ILeaseSigner
{
    string Sign( string payload );
}
