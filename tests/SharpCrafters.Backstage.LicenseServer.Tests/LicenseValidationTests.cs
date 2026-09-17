using PostSharp.LicenseServer.Tests.Infrastructure;

namespace PostSharp.LicenseServer.Tests;

/// <summary>
/// Why a license may refuse to serve a request. Each reason is reported back to the developer in the
/// body of the 403 response, so the wording matters.
/// </summary>
public sealed class LicenseValidationTests
{
    private static async Task<(Lease? Lease, Dictionary<int, string> Errors)> RequestAsync(
        LicenseServerTestContext context,
        License license,
        Version? version = null,
        DateTime? buildDate = null )
    {
        Dictionary<int, string> errors = [];

        Lease? lease = await context.LeaseService.GetLeaseAsync(
            version ?? new Version( 2025, 1, 0 ),
            buildDate,
            "desktop-1",
            "alice",
            "alice",
            TestClock.Origin,
            errors,
            [license] );

        return (lease, errors);
    }

    [Fact]
    public async Task UnparseableKey_IsReportedAsInvalid()
    {
        await using LicenseServerTestContext context = await LicenseServerTestContext.CreateAsync();

        // Added straight to the database, so the fake parser has no entry for its key.
        License license = new()
        {
            LicenseId = 99,
            LicenseKey = "NOT-A-KEY",
            ProductCode = "Ultimate",
            CreatedOn = TestClock.Origin
        };

        context.Db.Licenses.Add( license );
        await context.Db.SaveChangesAsync();

        var (lease, errors) = await RequestAsync( context, license );

        Assert.Null( lease );
        Assert.Equal( "The license key #99 is invalid.", errors[99] );
    }

    [Fact]
    public async Task LicenseNeedsANewerLicenseServer_SaysSo()
    {
        await using LicenseServerTestContext context = await LicenseServerTestContext.CreateAsync();

        License license = LicenseBuilder.Default()
            .WithMinPostSharpVersion( new Version( 2099, 3, 7 ) )
            .AddTo( context );

        var (lease, errors) = await RequestAsync( context, license );

        Assert.Null( lease );
        Assert.Contains( "requires higher version of PostSharp on the License Server", errors[1], StringComparison.Ordinal );
        Assert.Contains( "2099.3.7", errors[1], StringComparison.Ordinal );
    }

    [Fact]
    public async Task ClientIsOlderThanTheLicenseRequires_SaysSo()
    {
        await using LicenseServerTestContext context = await LicenseServerTestContext.CreateAsync();

        License license = LicenseBuilder.Default()
            .WithMinPostSharpVersion( new Version( 2024, 0, 0 ) )
            .AddTo( context );

        var (lease, errors) = await RequestAsync( context, license, new Version( 6, 5, 4 ) );

        Assert.Null( lease );
        Assert.Contains( "requires PostSharp version >= 2024.0.0", errors[1], StringComparison.Ordinal );
        Assert.Contains( "the requested version is 6.5.4", errors[1], StringComparison.Ordinal );
    }

    [Fact]
    public async Task LicenseNotEligibleForALicenseServer_SaysSo()
    {
        await using LicenseServerTestContext context = await LicenseServerTestContext.CreateAsync();
        License license = LicenseBuilder.Default().NotLicenseServerEligible().AddTo( context );

        var (lease, errors) = await RequestAsync( context, license );

        Assert.Null( lease );
        Assert.Contains( "cannot be used in the license server", errors[1], StringComparison.Ordinal );
    }

    [Fact]
    public async Task BuildIsNewerThanTheSubscription_SaysSoWithTheRequestedVersion()
    {
        await using LicenseServerTestContext context = await LicenseServerTestContext.CreateAsync();

        License license = LicenseBuilder.Default()
            .WithSubscriptionEndDate( TestClock.Days( -30 ) )
            .AddTo( context );

        var (lease, errors) = await RequestAsync( context, license, new Version( 2025, 1, 0 ), TestClock.Origin );

        Assert.Null( lease );
        Assert.Contains( "maintenance subscription of license #1 ends on", errors[1], StringComparison.Ordinal );
        Assert.Contains( "the requested version 2025.1.0", errors[1], StringComparison.Ordinal );
    }

    /// <summary>
    /// Clients older than PostSharp 5 do not send their version, so the message cannot mention one.
    /// </summary>
    [Fact]
    public async Task BuildIsNewerThanTheSubscriptionOnAnOldClient_OmitsTheVersion()
    {
        await using LicenseServerTestContext context = await LicenseServerTestContext.CreateAsync();

        License license = LicenseBuilder.Default()
            .WithSubscriptionEndDate( TestClock.Days( -30 ) )
            .AddTo( context );

        var (lease, errors) = await RequestAsync( context, license, new Version( 4, 9, 9 ), TestClock.Origin );

        Assert.Null( lease );
        Assert.Contains( "but the requested version has been built on", errors[1], StringComparison.Ordinal );
        Assert.DoesNotContain( "the requested version 4.9.9", errors[1], StringComparison.Ordinal );
    }

    [Fact]
    public async Task BuildExactlyOnTheSubscriptionEndDate_IsAccepted()
    {
        await using LicenseServerTestContext context = await LicenseServerTestContext.CreateAsync();

        License license = LicenseBuilder.Default()
            .WithSubscriptionEndDate( TestClock.Origin )
            .AddTo( context );

        var (lease, _) = await RequestAsync( context, license, buildDate: TestClock.Origin );

        Assert.NotNull( lease );
    }

    [Fact]
    public async Task NoBuildDate_SkipsTheSubscriptionCheck()
    {
        await using LicenseServerTestContext context = await LicenseServerTestContext.CreateAsync();

        License license = LicenseBuilder.Default()
            .WithSubscriptionEndDate( TestClock.Days( -30 ) )
            .AddTo( context );

        var (lease, _) = await RequestAsync( context, license );

        Assert.NotNull( lease );
    }

    [Fact]
    public async Task NoSubscriptionEndDate_SkipsTheSubscriptionCheck()
    {
        await using LicenseServerTestContext context = await LicenseServerTestContext.CreateAsync();
        License license = LicenseBuilder.Default().WithSubscriptionEndDate( null ).AddTo( context );

        var (lease, _) = await RequestAsync( context, license, buildDate: TestClock.Days( 1000 ) );

        Assert.NotNull( lease );
    }
}
