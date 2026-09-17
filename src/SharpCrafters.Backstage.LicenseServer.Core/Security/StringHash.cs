using System.Security.Cryptography;
using System.Text;

namespace SharpCrafters.Backstage.LicenseServer.Security;

/// <summary>
/// The unkeyed 64-bit hash that anonymises a user or a machine name in the audit log.
/// </summary>
/// <remarks>
/// <para>
/// This is the algorithm of <c>CryptoUtilities.ComputeStringHash64</c> in the PostSharp SDK and of
/// <c>HashUtilities.ComputeStringHash64</c> in SharpCrafters.Backstage. It is reproduced here rather
/// than called, because the method of Backstage is internal to that package.
/// </para>
/// <para>
/// The values are part of the audit-log format: customers archive the exported files and compare
/// them across years, and the license audit of Backstage hashes the same names the same way, so
/// that one person is counted once whatever the mixture of products they use. Changing the
/// algorithm would break both. <c>LeaseAuditLineTests</c> pins the values.
/// </para>
/// <para>
/// MD5 is not chosen for its cryptographic properties, which are irrelevant to an unkeyed
/// anonymising hash, but because the values have to equal the ones PostSharp has been producing
/// since 2013.
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

        // The name is normalized first, so that the same person is hashed to the same value whatever
        // the case and the padding of the name the client sent.
        byte[] bytes = Encoding.UTF8.GetBytes( value.Trim().ToLowerInvariant().Normalize() );

#pragma warning disable CA5350, CA5351 // MD5 is required to reproduce the values of PostSharp.
        byte[] hash = MD5.HashData( bytes );
#pragma warning restore CA5350, CA5351

        // The first eight bytes read as a little-endian signed integer. The bytes are combined
        // explicitly rather than reinterpreted, so that the value does not depend on the endianness
        // of the platform.
        long hash64 = 0;

        for ( int i = 7; i >= 0; i-- )
        {
            hash64 = (hash64 << 8) | hash[i];
        }

        // A non-null string never hashes to the value that stands for null.
        return hash64 == 0 ? -1 : hash64;
    }
}
