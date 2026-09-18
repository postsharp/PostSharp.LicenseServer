// Copyright (c) SharpCrafters s.r.o. See the LICENSE.md file in the root directory of this repository root for details.

using SharpCrafters.Backstage.LicenseServer.Licensing;
using SharpCrafters.Backstage.LicenseServer.Services;
using SharpCrafters.Backstage.LicenseServer.Tests.Infrastructure;
using SharpCrafters.Backstage.Licensing;

namespace SharpCrafters.Backstage.LicenseServer.Tests;

/// <summary>
/// The <c>ProductCode</c> column of an installation that has been upgraded holds the product names of
/// the PostSharp SDK, while the clients of the Backstage generation ask by the names of the Backstage
/// enumeration. A request that names a product has to find the licenses under either spelling.
/// </summary>
public sealed class ProductCodesTests
{
    [Theory]
    [InlineData( LicenseProduct.PostSharpUltimate, "PostSharpUltimate", "Ultimate" )]
    [InlineData( LicenseProduct.PostSharpFramework, "PostSharpFramework", "Framework" )]
    [InlineData( LicenseProduct.PostSharpCachingLibrary, "PostSharpCachingLibrary", "CachingLibrary" )]
    [InlineData( LicenseProduct.PostSharpDiagnosticsLibrary, "PostSharpDiagnosticsLibrary", "DiagnosticsLibrary" )]
    [InlineData( LicenseProduct.PostSharpModelLibrary, "PostSharpModelLibrary", "ModelLibrary" )]
    [InlineData( LicenseProduct.PostSharpThreadingLibrary, "PostSharpThreadingLibrary", "ThreadingLibrary" )]
    public void RenamedProduct_IsMatchedUnderBothSpellings( LicenseProduct product, string current, string legacy )
    {
        Assert.Equal( current, ProductCodes.ForStorage( product ) );
        Assert.Equal( [current, legacy], ProductCodes.Matching( current ) );
        Assert.Equal( [legacy, current], ProductCodes.Matching( legacy ) );
    }

    /// <summary>
    /// The products created after the renaming, and every value that the server does not know, match
    /// only themselves.
    /// </summary>
    [Theory]
    [InlineData( "MetalamaProfessional" )]
    [InlineData( "PostSharpEssentials" )]
    [InlineData( "SomethingNobodyHasHeardOf" )]
    public void ProductWithOneSpelling_IsMatchedByItself( string productCode ) => Assert.Equal( [productCode], ProductCodes.Matching( productCode ) );

    /// <summary>
    /// The server serves a license added by an earlier version to a client that asks for the same
    /// product by its Backstage name. Without the mapping, the server would deny the request while
    /// the license is present, which is what an upgraded installation would observe.
    /// </summary>
    [Fact]
    public async Task LicenseStoredUnderTheLegacyName_IsServedToAClientAskingForTheBackstageName()
    {
        await using var context = await LicenseServerTestContext.CreateAsync();
        LicenseBuilder.Default().WithProduct( "Ultimate" ).WithUsers( 5 ).AddTo( context );

        var lease = await context.LeaseService.GetLicenseLeaseAsync(
            "PostSharpUltimate",
            new Version( 2025, 1, 0 ),
            null,
            "desktop-1",
            "alice",
            "alice",
            TestClock.Origin,
            [] );

        Assert.NotNull( lease );
    }

    /// <summary>
    /// The mapping adds the other spelling of a product to a request. It does not make one product
    /// match another product.
    /// </summary>
    [Fact]
    public async Task LicenseOfAnotherProduct_IsStillNotServed()
    {
        await using var context = await LicenseServerTestContext.CreateAsync();
        LicenseBuilder.Default().WithProduct( "Ultimate" ).WithUsers( 5 ).AddTo( context );

        var lease = await context.LeaseService.GetLicenseLeaseAsync(
            "PostSharpFramework",
            new Version( 2025, 1, 0 ),
            null,
            "desktop-1",
            "alice",
            "alice",
            TestClock.Origin,
            [] );

        Assert.Null( lease );
    }
}