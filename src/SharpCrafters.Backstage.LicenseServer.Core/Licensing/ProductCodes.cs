using System.Collections.Immutable;
using SharpCrafters.Backstage.Licensing;

namespace SharpCrafters.Backstage.LicenseServer.Licensing;

/// <summary>
/// The value of the <c>ProductCode</c> column, and the names a client may ask for it by.
/// </summary>
/// <remarks>
/// <para>
/// The column contains the name of the licensed product. A client that names a product in its
/// request is served only from the licenses that carry that name. The two spellings of a name must
/// be treated as one name, because PostSharp and SharpCrafters.Backstage use different ones. The
/// enumeration of PostSharp calls the products <c>Ultimate</c> and <c>Framework</c>. The enumeration
/// of Backstage calls the same values <c>PostSharpUltimate</c> and <c>PostSharpFramework</c>.
/// </para>
/// <para>
/// A database created by an earlier version of this server therefore contains the PostSharp
/// spelling, and a client of the Backstage generation asks for the Backstage spelling. Without this
/// mapping, a request that names a product would find none of the licenses of an existing
/// installation. The server writes new rows with the Backstage spelling, which is the spelling the
/// clients send, so a database that went through the upgrade contains both. The two spellings are
/// therefore reconciled when a request is matched, and not when a row is written.
/// </para>
/// </remarks>
public static class ProductCodes
{
    /// <summary>
    /// The name that each product had in the <c>LicensedProduct</c> enumeration of the PostSharp
    /// SDK, for the products whose name changed. The table contains neither the values that a
    /// license server never wrote, nor the Metalama products, which are more recent than the change.
    /// </summary>
    private static readonly ImmutableDictionary<string, string> legacyNames =
        new Dictionary<string, string>( StringComparer.OrdinalIgnoreCase )
        {
            [nameof(LicenseProduct.PostSharpUltimate)] = "Ultimate",
            [nameof(LicenseProduct.PostSharpFramework)] = "Framework",
            [nameof(LicenseProduct.PostSharpDiagnosticsLibrary)] = "DiagnosticsLibrary",
            [nameof(LicenseProduct.PostSharpModelLibrary)] = "ModelLibrary",
            [nameof(LicenseProduct.PostSharpThreadingLibrary)] = "ThreadingLibrary",
            [nameof(LicenseProduct.PostSharpCachingLibrary)] = "CachingLibrary",
#pragma warning disable CS0618 // The product is obsolete, but a row naming it may still exist.
            [nameof(LicenseProduct.PostSharpUltimate1)] = "PostSharp30"
#pragma warning restore CS0618
        }.ToImmutableDictionary( StringComparer.OrdinalIgnoreCase );

    private static readonly ImmutableDictionary<string, string> currentNames =
        legacyNames.ToImmutableDictionary( x => x.Value, x => x.Key, StringComparer.OrdinalIgnoreCase );

    /// <summary>
    /// Gets the value to store in the <c>ProductCode</c> column for a product.
    /// </summary>
    public static string ForStorage( LicenseProduct product ) => product.ToString();

    /// <summary>
    /// Gets every value of the <c>ProductCode</c> column that satisfies a request for a product.
    /// These values are the name that the client asked for and, when the name changed, its other
    /// spelling.
    /// </summary>
    public static IReadOnlyList<string> Matching( string productCode )
    {
        if ( legacyNames.TryGetValue( productCode, out string? legacy ) )
        {
            return [productCode, legacy];
        }

        if ( currentNames.TryGetValue( productCode, out string? current ) )
        {
            return [productCode, current];
        }

        return [productCode];
    }
}
