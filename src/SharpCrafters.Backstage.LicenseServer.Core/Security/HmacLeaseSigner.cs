using System.Security.Cryptography;
using System.Text;

namespace SharpCrafters.Backstage.LicenseServer.Security;

/// <summary>
/// Signs the audit log with HMAC-SHA256.
/// </summary>
/// <remarks>
/// The legacy implementation called the parameterless <c>HMAC.Create()</c>. That method raises
/// <see cref="PlatformNotSupportedException"/> on .NET 5 and later. On .NET Framework, it produced
/// an HMAC-SHA1 under a key that it generated at random at every call, so the audit chain could not
/// be verified. A key that the server stores makes the chain verifiable. The base64 representation
/// of a SHA-256 hash has 44 characters, which the existing <c>varchar(100)</c> column accepts, so
/// the schema does not change.
/// </remarks>
public sealed class HmacLeaseSigner( IAuditKeyProvider keyProvider ) : ILeaseSigner
{
    public string Sign( string payload )
    {
        ArgumentNullException.ThrowIfNull( payload );

        return Convert.ToBase64String(
            HMACSHA256.HashData( keyProvider.GetKey(), Encoding.UTF8.GetBytes( payload ) ) );
    }
}
