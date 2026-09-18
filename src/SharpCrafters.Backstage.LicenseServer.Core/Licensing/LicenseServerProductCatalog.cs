// Copyright (c) SharpCrafters s.r.o. See the LICENSE.md file in the root directory of this repository root for details.

using System.Collections.Immutable;
using SharpCrafters.Backstage.Licensing;

namespace SharpCrafters.Backstage.LicenseServer.Licensing;

/// <summary>
/// Names the products whose license keys the server holds.
/// </summary>
/// <remarks>
/// <para>
/// A <see cref="LicenseProductCatalog"/> answers two kinds of question. The first kind is the name
/// of a product. The second kind is how a license key of that product is registered on the machine
/// of a developer. Only the first kind applies here, because a license server serves the license
/// keys that its administrator added and registers none. The base class answers the first kind for
/// every product of both families, so this class only declares that the server is not a product
/// family of its own.
/// </para>
/// <para>
/// The members of the second kind let a client decide which edition to offer, which license key to
/// keep when another one is registered, and where to store it. A server makes none of these
/// decisions, so these members raise an exception instead of returning a value that would be wrong.
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
    /// A server holds the license keys of the products that its administrator added, and it can hold
    /// the products of both families at the same time.
    /// </summary>
    public override bool IsProductOfFamily( LicenseProduct product ) => true;

    /// <inheritdoc />
    public override bool IsFreeProduct( LicenseProduct product ) => product is LicenseProduct.MetalamaCommunity or LicenseProduct.PostSharpEssentials;

    /// <summary>
    /// The server stores every license key in the same table. The registration of a client, which
    /// depends on the version, has no equivalent here.
    /// </summary>
    public override bool RequiresVersionSpecificRegistration( LicenseProduct product ) => false;

    /// <inheritdoc />
    public override ImmutableArray<LicenseProduct> GetProductsCoexistingWith( LicenseProduct product ) => throw new NotSupportedException( notAClient );

    /// <inheritdoc />
    public override string PremiumEditionDisplayName => throw new NotSupportedException( notAClient );

    /// <inheritdoc />
    public override LicenseProduct EvaluationProduct => throw new NotSupportedException( notAClient );

    /// <inheritdoc />
    public override LicenseProduct? CommunityProduct => throw new NotSupportedException( notAClient );

    /// <inheritdoc />
    public override LicenseProduct? LegacyFreeProduct => throw new NotSupportedException( notAClient );
}