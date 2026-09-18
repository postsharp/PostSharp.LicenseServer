using System.Security.Cryptography;
using System.Text;

namespace SharpCrafters.Backstage.LicenseServer.Security;

/// <summary>
/// The unkeyed 64-bit hash that anonymizes a user name or a machine name in the audit log.
/// </summary>
/// <remarks>
/// <para>
/// This is the algorithm of <c>CryptoUtilities.ComputeStringHash64</c> in the PostSharp SDK and of
/// <c>HashUtilities.ComputeStringHash64</c> in SharpCrafters.Backstage. It is reproduced here and
/// not called, because the method of Backstage is internal to that package.
/// </para>
/// <para>
/// The values are part of the format of the audit log. Customers archive the exported files and
/// compare them across years, and the license audit of Backstage hashes the same names in the same
/// way, so that one person is counted once whatever the products they use. A change to the algorithm
/// would break both. <c>LeaseAuditLineTests</c> verifies the values.
/// </para>
/// <para>
/// MD5 is not used for its cryptographic properties, which are irrelevant to an unkeyed anonymizing
/// hash. It is used because the values must be equal to the values PostSharp has produced since
/// 2013.
/// </para>
/// </remarks>
public static class StringHash
{
    /// <summary>
    /// Computes the hash of a string, or <c>0</c> when it is <c>null</c>.
    /// </summary>
    public static long ComputeStringHash64( string? value )
    {
        if ( value == null )
        {
            return 0;
        }

        // The name is normalized first, so that the same person produces the same value whatever the
        // case and the whitespace of the name that the client sent.
        byte[] bytes = Encoding.UTF8.GetBytes( value.Trim().ToLowerInvariant().Normalize() );

#pragma warning disable CA5350, CA5351 // MD5 is required to reproduce the values of PostSharp.
        byte[] hash = MD5.HashData( bytes );
#pragma warning restore CA5350, CA5351

        // The first eight bytes, read as a little-endian signed integer. The bytes are combined
        // explicitly and not reinterpreted, so that the value does not depend on the byte order of
        // the platform.
        long hash64 = 0;

        for ( int i = 7; i >= 0; i-- )
        {
            hash64 = (hash64 << 8) | hash[i];
        }

        // A string that is not null never produces the value that represents null.
        return hash64 == 0 ? -1 : hash64;
    }
}
