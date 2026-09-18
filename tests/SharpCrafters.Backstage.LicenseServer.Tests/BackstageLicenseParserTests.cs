// Copyright (c) SharpCrafters s.r.o. See the LICENSE.md file in the root directory of this repository root for details.

using SharpCrafters.Backstage.LicenseServer.Licensing;
using SharpCrafters.Backstage.LicenseServer.Tests.Infrastructure;
using SharpCrafters.Backstage.Licensing;
using SharpCrafters.Backstage.Licensing.Licenses;

namespace SharpCrafters.Backstage.LicenseServer.Tests;

/// <summary>
/// The licensing component, which is SharpCrafters.Backstage. Every other test of this suite runs
/// against <c>FakeLicenseParser</c>. These tests are the only ones that parse a real license key,
/// and therefore the only ones that detect a change in the values the package reports.
/// </summary>
public sealed class BackstageLicenseParserTests
{
    private static readonly BackstageLicenseParser parser = new( TestLicenseKeys.Authority );

    [Fact]
    public void SignedKey_IsParsedIntoTheFactsTheServerNeeds()
    {
        var builder = TestLicenseKeys.Builder( licenseId: 4242 );
        builder.UserNumber = 25;
        builder.ValidTo = new DateTime( 2030, 6, 30, 0, 0, 0, DateTimeKind.Utc );
        builder.SubscriptionEndDate = new DateTime( 2029, 6, 30, 0, 0, 0, DateTimeKind.Utc );
        builder.GraceDays = 7;
        builder.GracePercent = 15;

        var license = parser.TryParse( builder.Sign() );

        Assert.NotNull( license );
        Assert.Equal( 4242, license.LicenseId );
        Assert.Equal( "PostSharpUltimate", license.Product );

        // LicenseType.Business and LicenseType.PerUser are two names of the same value, and PerUser is
        // the one declared first, so it is the one the enumeration formats.
        Assert.Equal( "PerUser", license.LicenseType );
        Assert.Equal( 25, license.UserNumber );
        Assert.Equal( new DateTime( 2030, 6, 30 ), license.ValidTo );
        Assert.Equal( new DateTime( 2029, 6, 30 ), license.SubscriptionEndDate );
        Assert.Equal( 7, license.GraceDays );
        Assert.Equal( 15, license.GracePercent );
        Assert.True( license.IsLicenseServerEligible );
        Assert.Equal( "Business License", license.LicenseTypeName );
        Assert.Equal( "PostSharp Ultimate", license.ProductName );
    }

    /// <summary>
    /// The parser also reads a license key that SharpCrafters.Backstage creates for its own tests.
    /// The server and the package therefore agree on the whole format, and not only on the fields
    /// that a key built in this file uses.
    /// </summary>
    [Fact]
    public void KeyIssuedByTheBackstageTestProvider_IsParsed()
    {
        var license = parser.TryParse( TestLicenseKeys.Keys.PostSharpUltimate );

        Assert.NotNull( license );
        Assert.Equal( "PostSharpUltimate", license.Product );
        Assert.True( license.IsLicenseServerEligible );
    }

    /// <summary>
    /// The signature prevents a customer from creating their own licenses, so the server must not
    /// serve a key signed with a key that the authority does not hold.
    /// </summary>
    [Fact]
    public void KeySignedWithAForgedKey_IsRejected() => Assert.Null( parser.TryParse( TestLicenseKeys.Builder().SignWithAForgedKey() ) );

    /// <summary>
    /// A key whose signature names an authority that was never issued is an invalid key, and not an
    /// exception. The provider raises an exception when it is asked for a key it does not hold, and
    /// an administrator pastes license keys into a web form.
    /// </summary>
    [Fact]
    public void KeySignedByAnUnknownAuthority_IsRejectedWithoutThrowing()
        => Assert.Null( parser.TryParse( TestLicenseKeys.Builder().SignWithAnUnknownAuthority() ) );

    [Fact]
    public void UnsignedKeyOfATypeThatRequiresASignature_IsRejected() => Assert.Null( parser.TryParse( TestLicenseKeys.Builder().Unsigned() ) );

    /// <summary>
    /// An evaluation key carries no signature by design, and the server may serve it.
    /// </summary>
    [Fact]
    public void UnsignedKeyOfATypeThatRequiresNoSignature_IsParsed()
    {
        var key = TestLicenseKeys.Builder( licenseType: LicenseType.Evaluation ).Unsigned();

        var license = parser.TryParse( key );

        Assert.NotNull( license );
        Assert.Equal( nameof(LicenseType.Evaluation), license.LicenseType );
    }

    [Theory]
    [InlineData( "" )]
    [InlineData( "   " )]
    [InlineData( "NOT-A-KEY" )]
    [InlineData( "1-AAAAAAAA" )]
    [InlineData( "no-hyphen-prefix" )]
    public void MalformedKey_IsRejected( string key ) => Assert.Null( parser.TryParse( key ) );

    /// <summary>
    /// A key that carries no grace percentage is served with the percentage PostSharp applied, so a
    /// license that was being served before the migration is served the same way after it.
    /// </summary>
    [Fact]
    public void KeyWithoutAGracePercentage_GetsThirtyPercent()
    {
        var builder = TestLicenseKeys.Builder();
        builder.GracePercent = null;

        Assert.Equal( 30, parser.TryParse( builder.Sign() )!.GracePercent );
    }

    /// <summary>
    /// A key that carries no grace period gets the thirty days that SharpCrafters.Backstage applies.
    /// </summary>
    /// <remarks>
    /// This is a change of behaviour. The PostSharp SDK returned zero days here, so such a license
    /// denied a request as soon as its capacity was exceeded, while it now keeps serving for thirty
    /// days beyond capacity. The default is applied inside <c>LicenseKeyData.GraceDays</c>, whose type
    /// is not nullable, so the parser cannot tell an absent field from a field set to thirty and
    /// cannot restore the old value. Keys issued with an explicit grace period are unaffected.
    /// </remarks>
    [Fact]
    public void KeyWithoutAGracePeriod_GetsThirtyDays()
    {
        var builder = TestLicenseKeys.Builder();

        Assert.Equal( 30, parser.TryParse( builder.Sign() )!.GraceDays );
    }

    /// <summary>
    /// A key that carries no minimal version of the client is not served to every client. The
    /// licensing library derives the version from the other fields. A key that a license server may
    /// serve is readable by PostSharp 5.0.22 and later, which is the version that introduced the
    /// license server.
    /// </summary>
    [Fact]
    public void MinPostSharpVersion_IsDerivedWhenTheKeyDoesNotDeclareIt()
    {
        var license = parser.TryParse( TestLicenseKeys.Builder().Sign() );

        Assert.NotNull( license );
        Assert.Equal( new Version( 5, 0, 22 ), license.MinPostSharpVersion );
    }

    /// <summary>
    /// The product name stored in the database is the one of the Backstage enumeration, which is the
    /// one a client of that generation asks for. <see cref="ProductCodesTests"/> covers the licenses
    /// that a previous version of this server stored under the PostSharp spelling.
    /// </summary>
    [Fact]
    public void Product_IsStoredUnderItsBackstageName()
    {
        var license = parser.TryParse( TestLicenseKeys.Builder( product: LicenseProduct.PostSharpFramework ).Sign() );

        Assert.Equal( "PostSharpFramework", license!.Product );
    }

    /// <summary>
    /// A key that says it may not be leased is not served, whatever else it carries.
    /// </summary>
    [Fact]
    public void KeyThatIsNotEligibleForALicenseServer_SaysSo()
    {
        var builder = TestLicenseKeys.Builder();
        builder.LicenseServerEligible = false;

        Assert.False( parser.TryParse( builder.Sign() )!.IsLicenseServerEligible );
    }

    [Theory]
    [InlineData( "  1-ABCDEF  ", "1-ABCDEF" )]
    [InlineData( "1-ABC DEF", "1-ABCDEF" )]
    [InlineData( "1-ABC\r\n\tDEF", "1-ABCDEF" )]
    public void CleanLicenseString_RemovesEveryKindOfWhitespace( string pasted, string expected )
        => Assert.Equal( expected, parser.CleanLicenseString( pasted ) );

    /// <summary>
    /// A key pasted with the whitespace that an e-mail adds is parsed after it is cleaned. The page
    /// that adds a license performs these two steps in this order.
    /// </summary>
    [Fact]
    public void PastedKey_ParsesAfterCleaning()
    {
        var key = TestLicenseKeys.Builder().Sign();
        var pasted = key[..10] + " \r\n " + key[10..];

        Assert.Null( parser.TryParse( pasted ) );
        Assert.NotNull( parser.TryParse( parser.CleanLicenseString( pasted ) ) );
    }
}