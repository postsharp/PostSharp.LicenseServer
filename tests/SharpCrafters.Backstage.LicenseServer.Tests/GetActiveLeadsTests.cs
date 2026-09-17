using SharpCrafters.Backstage.LicenseServer.Tests.Infrastructure;

namespace SharpCrafters.Backstage.LicenseServer.Tests;

/// <summary>
/// How many seats of a license are in use at a given moment.
/// </summary>
public sealed class GetActiveLeadsTests
{
    [Fact]
    public async Task GetActiveLeads_NoLeases_ReturnsZero()
    {
        await using LicenseServerTestContext context = await LicenseServerTestContext.CreateAsync();
        License license = LicenseBuilder.Default().AddTo( context );

        Assert.Equal( 0, context.Repository.GetActiveLeads( license.LicenseId, TestClock.Days( 1 ) ) );
    }

    [Fact]
    public async Task GetActiveLeads_OneUserOneMachine_ReturnsOne()
    {
        await using LicenseServerTestContext context = await LicenseServerTestContext.CreateAsync();
        License license = LicenseBuilder.Default().AddTo( context );
        LeaseBuilder.For( license ).User( "alice" ).Machine( "desktop-1" ).AddTo( context );

        Assert.Equal( 1, context.Repository.GetActiveLeads( license.LicenseId, TestClock.Days( 1 ) ) );
    }

    [Fact]
    public async Task GetActiveLeads_OneUserTwoMachines_StillReturnsOne()
    {
        await using LicenseServerTestContext context = await LicenseServerTestContext.CreateAsync();
        License license = LicenseBuilder.Default().AddTo( context );
        LeaseBuilder.For( license ).User( "alice" ).Machine( "desktop-1" ).AddTo( context );
        LeaseBuilder.For( license ).User( "alice" ).Machine( "laptop-1" ).AddTo( context );

        Assert.Equal( 1, context.Repository.GetActiveLeads( license.LicenseId, TestClock.Days( 1 ) ) );
    }

    [Fact]
    public async Task GetActiveLeads_OneUserThreeMachines_ReturnsTwo()
    {
        await using LicenseServerTestContext context = await LicenseServerTestContext.CreateAsync();
        License license = LicenseBuilder.Default().AddTo( context );

        foreach ( string machine in new[] { "desktop-1", "laptop-1", "desktop-2" } )
        {
            LeaseBuilder.For( license ).User( "alice" ).Machine( machine ).AddTo( context );
        }

        Assert.Equal( 2, context.Repository.GetActiveLeads( license.LicenseId, TestClock.Days( 1 ) ) );
    }

    [Fact]
    public async Task GetActiveLeads_TwoUsers_ReturnsTwo()
    {
        await using LicenseServerTestContext context = await LicenseServerTestContext.CreateAsync();
        License license = LicenseBuilder.Default().AddTo( context );
        LeaseBuilder.For( license ).User( "alice" ).AddTo( context );
        LeaseBuilder.For( license ).User( "bob" ).AddTo( context );

        Assert.Equal( 2, context.Repository.GetActiveLeads( license.LicenseId, TestClock.Days( 1 ) ) );
    }

    [Fact]
    public async Task GetActiveLeads_LeaseStartingExactlyNow_IsCounted()
    {
        await using LicenseServerTestContext context = await LicenseServerTestContext.CreateAsync();
        License license = LicenseBuilder.Default().AddTo( context );
        LeaseBuilder.For( license ).From( TestClock.Origin ).Lasting( 3 ).AddTo( context );

        Assert.Equal( 1, context.Repository.GetActiveLeads( license.LicenseId, TestClock.Origin ) );
    }

    [Fact]
    public async Task GetActiveLeads_LeaseEndingExactlyNow_IsNotCounted()
    {
        await using LicenseServerTestContext context = await LicenseServerTestContext.CreateAsync();
        License license = LicenseBuilder.Default().AddTo( context );
        LeaseBuilder.For( license ).From( TestClock.Origin ).Lasting( 3 ).AddTo( context );

        Assert.Equal( 0, context.Repository.GetActiveLeads( license.LicenseId, TestClock.Days( 3 ) ) );
    }

    [Fact]
    public async Task GetActiveLeads_OtherLicense_IsNotCounted()
    {
        await using LicenseServerTestContext context = await LicenseServerTestContext.CreateAsync();
        License first = LicenseBuilder.Default().WithLicenseId( 1 ).AddTo( context );
        License second = LicenseBuilder.Default().WithLicenseId( 2 ).AddTo( context );

        LeaseBuilder.For( second ).AddTo( context );

        Assert.Equal( 0, context.Repository.GetActiveLeads( first.LicenseId, TestClock.Days( 1 ) ) );
        Assert.Equal( 1, context.Repository.GetActiveLeads( second.LicenseId, TestClock.Days( 1 ) ) );
    }

    [Fact]
    public async Task GetActiveLeads_ReplacedLease_IsNotCounted()
    {
        await using LicenseServerTestContext context = await LicenseServerTestContext.CreateAsync();
        License license = LicenseBuilder.Default().AddTo( context );
        Lease original = LeaseBuilder.For( license ).AddTo( context );

        context.Repository.CancelLease( original, "admin", TestClock.Days( 1 ) );
        await context.Repository.SaveChangesAsync();

        Assert.Equal( 0, context.Repository.GetActiveLeads( license.LicenseId, TestClock.Days( 2 ) ) );
    }

    /// <summary>
    /// SQL Server's default collation is case-insensitive. The in-memory database is configured to
    /// match, so that a test cannot pass here and fail in production.
    /// </summary>
    [Fact]
    public async Task GetActiveLeads_UserNameCasingDiffers_CountsAsOneUser()
    {
        await using LicenseServerTestContext context = await LicenseServerTestContext.CreateAsync();
        License license = LicenseBuilder.Default().AddTo( context );
        LeaseBuilder.For( license ).User( "alice" ).Machine( "desktop-1" ).AddTo( context );
        LeaseBuilder.For( license ).User( "ALICE" ).Machine( "laptop-1" ).AddTo( context );

        Assert.Equal( 1, context.Repository.GetActiveLeads( license.LicenseId, TestClock.Days( 1 ) ) );
    }

    [Fact]
    public async Task GetActiveLeads_HonoursMachinesPerUser()
    {
        await using LicenseServerTestContext context =
            await LicenseServerTestContext.CreateAsync( o => o.MachinesPerUser = 1 );

        License license = LicenseBuilder.Default().AddTo( context );
        LeaseBuilder.For( license ).User( "alice" ).Machine( "desktop-1" ).AddTo( context );
        LeaseBuilder.For( license ).User( "alice" ).Machine( "laptop-1" ).AddTo( context );

        // With one machine per seat, the same user on two machines consumes two seats.
        Assert.Equal( 2, context.Repository.GetActiveLeads( license.LicenseId, TestClock.Days( 1 ) ) );
    }
}
