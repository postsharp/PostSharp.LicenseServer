namespace SharpCrafters.Backstage.LicenseServer.Security;

/// <summary>
/// Supplies the key used to sign the lease audit log.
/// </summary>
public interface IAuditKeyProvider
{
    byte[] GetKey();
}
