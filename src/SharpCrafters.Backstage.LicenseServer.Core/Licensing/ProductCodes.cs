using System.Collections.Immutable;
using SharpCrafters.Backstage.Licensing;

namespace SharpCrafters.Backstage.LicenseServer.Licensing;

/// <summary>
/// The value of the <c>ProductCode</c> column, and the names a client may ask for it by.
/// </summary>
/// <remarks>
/// <para>
/// The column holds the name of the licensed product, and a client that names a product in its
/// request is served only from the licenses that carry that name. The two spellings of a name have to
/// be treated as one, because PostSharp and SharpCrafters.Backstage do not agree on them: the
/// enumeration of PostSharp calls the products <c>Ultimate</c> and <c>Framework</c>, while the
/// enumeration of Backstage calls the same values <c>PostSharpUltimate</c> and
/// <c>PostSharpFramework</c>.
/// </para>
/// <para>
/// A database created by a previous version of this server therefore holds the PostSharp spelling,
/// while a client of the Backstage generation asks for the Backstage one. Without this mapping, a
/// request that names a product would find none of the licenses an existing installation holds. New
/// rows are written with the Backstage spelling, which is the one the clients send, so a database
/// that has been through the upgrade holds both -- which is why the matching, and not the storage, is
/// where the two are reconciled.
/// </para>
/// </remarks>
public static class ProductCodes
{
    /// <summary>
    /// The name each product had in the <c>LicensedProduct</c> enumeration of the PostSharp SDK, for
    /// the products whose name changed. The values that were never written by a license server, and
    /// the Metalama products, which postdate the change, are absent.
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
    /// Gets every value of the <c>ProductCode</c> column that satisfies a request for a product,
    /// which is the name the client asked for and, when the name changed, the other spelling of it.
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
