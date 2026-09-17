namespace SharpCrafters.Backstage.LicenseServer.Security;

/// <summary>
/// Signs the lease audit log. Each lease is signed together with the signature of the previous
/// lease, so that a removed or altered row breaks the chain.
/// </summary>
public interface ILeaseSigner
{
    string Sign( string payload );
}
