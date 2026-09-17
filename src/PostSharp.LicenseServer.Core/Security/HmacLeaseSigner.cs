using System.Security.Cryptography;
using System.Text;

namespace PostSharp.LicenseServer.Security;

/// <summary>
/// Signs the audit log with HMAC-SHA256.
/// </summary>
/// <remarks>
/// The legacy implementation called the parameterless <c>HMAC.Create()</c>, which throws
/// <see cref="PlatformNotSupportedException"/> on .NET 5 and later, and which on .NET Framework
/// produced an HMAC-SHA1 under a <i>randomly generated key for every single call</i>. The audit
/// chain was therefore never verifiable by anyone. A configured key makes it verifiable.
/// The base64 of a SHA-256 hash is 44 characters, which fits the existing <c>varchar(100)</c>
/// column, so no schema change is required.
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
