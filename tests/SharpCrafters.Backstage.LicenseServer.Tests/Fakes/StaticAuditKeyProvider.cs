using System.Text;
using SharpCrafters.Backstage.LicenseServer.Security;

namespace SharpCrafters.Backstage.LicenseServer.Tests.Fakes;

/// <summary>
/// Supplies a constant audit signing key.
/// </summary>
public sealed class StaticAuditKeyProvider : IAuditKeyProvider
{
    private readonly byte[] key = Encoding.UTF8.GetBytes( "constant-test-key-for-audit-hmac" );

    public byte[] GetKey() => this.key;
}
