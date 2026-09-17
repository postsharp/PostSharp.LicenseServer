using System.Collections.Immutable;
using SharpCrafters.Backstage.Licensing;

namespace SharpCrafters.Backstage.LicenseServer.Licensing;

/// <summary>
/// Names the products whose license keys the server holds.
/// </summary>
/// <remarks>
/// <para>
/// A <see cref="LicenseProductCatalog"/> answers two kinds of question: what a product is called,
/// and how a license key of that product is registered on a developer's machine. Only the first
/// kind has a meaning here, because a license server serves the license keys its administrator
/// added and never registers one. The base class already answers it for every product of both
/// families, so this class only has to state that the server is not a product family of its own.
/// </para>
/// <para>
/// The members of the second kind exist so that a client can decide which edition to offer, which
/// license key to keep when another is registered, and where to store it. A server has no such
/// decisions to make, so they throw rather than return a value that would quietly be wrong.
/// </para>
/// </remarks>
public sealed class LicenseServerProductCatalog : LicenseProductCatalog
{
    private const string notAClient =
        "The license server names products but does not register license keys, so it has no "
        + "editions of its own.";

    /// <summary>
    /// Gets the single instance, which holds no state.
    /// </summary>
    public static LicenseServerProductCatalog Instance { get; } = new();

    private LicenseServerProductCatalog() { }

    /// <summary>
    /// A server pools the license keys of whatever products its administrator added, including the
    /// products of both families side by side.
    /// </summary>
    public override bool IsProductOfFamily( LicenseProduct product ) => true;

    /// <inheritdoc />
    public override bool IsFreeProduct( LicenseProduct product )
        => product is LicenseProduct.MetalamaCommunity or LicenseProduct.PostSharpEssentials;

    /// <summary>
    /// The server stores every license key in the same table, and the version-specific registration
    /// of a client has no equivalent.
    /// </summary>
    public override bool RequiresVersionSpecificRegistration( LicenseProduct product ) => false;

    /// <inheritdoc />
    public override ImmutableArray<LicenseProduct> GetProductsCoexistingWith( LicenseProduct product )
        => throw new NotSupportedException( notAClient );

    /// <inheritdoc />
    public override string PremiumEditionDisplayName => throw new NotSupportedException( notAClient );

    /// <inheritdoc />
    public override LicenseProduct EvaluationProduct => throw new NotSupportedException( notAClient );

    /// <inheritdoc />
    public override LicenseProduct? CommunityProduct => throw new NotSupportedException( notAClient );

    /// <inheritdoc />
    public override LicenseProduct? LegacyFreeProduct => throw new NotSupportedException( notAClient );
}
