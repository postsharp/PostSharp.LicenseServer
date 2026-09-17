using SharpCrafters.Backstage.LicenseServer.Tests.Infrastructure;

namespace SharpCrafters.Backstage.LicenseServer.Tests;

/// <summary>
/// How many seats of a license are in use at a given moment.
/// </summary>
public sealed class GetActiveSeatsTests
{
    [Fact]
    public async Task GetActiveSeats_NoLeases_ReturnsZero()
    {
        await using LicenseServerTestContext context = await LicenseServerTestContext.CreateAsync();
        License license = LicenseBuilder.Default().AddTo( context );

        Assert.Equal( 0, context.Repository.GetActiveSeats( license.LicenseId, TestClock.Days( 1 ) ) );
    }

    [Fact]
    public async Task GetActiveSeats_OneUserOneMachine_ReturnsOne()
    {
        await using LicenseServerTestContext context = await LicenseServerTestContext.CreateAsync();
        License license = LicenseBuilder.Default().AddTo( context );
        LeaseBuilder.For( license ).User( "alice" ).Machine( "desktop-1" ).AddTo( context );

        Assert.Equal( 1, context.Repository.GetActiveSeats( license.LicenseId, TestClock.Days( 1 ) ) );
    }

    [Fact]
    public async Task GetActiveSeats_OneUserTwoMachines_StillReturnsOne()
    {
        await using LicenseServerTestContext context = await LicenseServerTestContext.CreateAsync();
        License license = LicenseBuilder.Default().AddTo( context );
        LeaseBuilder.For( license ).User( "alice" ).Machine( "desktop-1" ).AddTo( context );
        LeaseBuilder.For( license ).User( "alice" ).Machine( "laptop-1" ).AddTo( context );

        Assert.Equal( 1, context.Repository.GetActiveSeats( license.LicenseId, TestClock.Days( 1 ) ) );
    }

    [Fact]
    public async Task GetActiveSeats_OneUserThreeMachines_ReturnsTwo()
    {
        await using LicenseServerTestContext context = await LicenseServerTestContext.CreateAsync();
        License license = LicenseBuilder.Default().AddTo( context );

        foreach ( string machine in new[] { "desktop-1", "laptop-1", "desktop-2" } )
        {
            LeaseBuilder.For( license ).User( "alice" ).Machine( machine ).AddTo( context );
        }

        Assert.Equal( 2, context.Repository.GetActiveSeats( license.LicenseId, TestClock.Days( 1 ) ) );
    }

    [Fact]
    public async Task GetActiveSeats_TwoUsers_ReturnsTwo()
    {
        await using LicenseServerTestContext context = await LicenseServerTestContext.CreateAsync();
        License license = LicenseBuilder.Default().AddTo( context );
        LeaseBuilder.For( license ).User( "alice" ).AddTo( context );
        LeaseBuilder.For( license ).User( "bob" ).AddTo( context );

        Assert.Equal( 2, context.Repository.GetActiveSeats( license.LicenseId, TestClock.Days( 1 ) ) );
    }

    [Fact]
    public async Task GetActiveSeats_LeaseStartingExactlyNow_IsCounted()
    {
        await using LicenseServerTestContext context = await LicenseServerTestContext.CreateAsync();
        License license = LicenseBuilder.Default().AddTo( context );
        LeaseBuilder.For( license ).From( TestClock.Origin ).Lasting( 3 ).AddTo( context );

        Assert.Equal( 1, context.Repository.GetActiveSeats( license.LicenseId, TestClock.Origin ) );
    }

    [Fact]
    public async Task GetActiveSeats_LeaseEndingExactlyNow_IsNotCounted()
    {
        await using LicenseServerTestContext context = await LicenseServerTestContext.CreateAsync();
        License license = LicenseBuilder.Default().AddTo( context );
        LeaseBuilder.For( license ).From( TestClock.Origin ).Lasting( 3 ).AddTo( context );

        Assert.Equal( 0, context.Repository.GetActiveSeats( license.LicenseId, TestClock.Days( 3 ) ) );
    }

    [Fact]
    public async Task GetActiveSeats_OtherLicense_IsNotCounted()
    {
        await using LicenseServerTestContext context = await LicenseServerTestContext.CreateAsync();
        License first = LicenseBuilder.Default().WithLicenseId( 1 ).AddTo( context );
        License second = LicenseBuilder.Default().WithLicenseId( 2 ).AddTo( context );

        LeaseBuilder.For( second ).AddTo( context );

        Assert.Equal( 0, context.Repository.GetActiveSeats( first.LicenseId, TestClock.Days( 1 ) ) );
        Assert.Equal( 1, context.Repository.GetActiveSeats( second.LicenseId, TestClock.Days( 1 ) ) );
    }

    [Fact]
    public async Task GetActiveSeats_ReplacedLease_IsNotCounted()
    {
        await using LicenseServerTestContext context = await LicenseServerTestContext.CreateAsync();
        License license = LicenseBuilder.Default().AddTo( context );
        Lease original = LeaseBuilder.For( license ).AddTo( context );

        context.Repository.CancelLease( original, "admin", TestClock.Days( 1 ) );
        await context.Repository.SaveChangesAsync();

        Assert.Equal( 0, context.Repository.GetActiveSeats( license.LicenseId, TestClock.Days( 2 ) ) );
    }

    /// <summary>
    /// SQL Server's default collation is case-insensitive. The in-memory database is configured to
    /// match, so that a test cannot pass here and fail in production.
    /// </summary>
    [Fact]
    public async Task GetActiveSeats_UserNameCasingDiffers_CountsAsOneUser()
    {
        await using LicenseServerTestContext context = await LicenseServerTestContext.CreateAsync();
        License license = LicenseBuilder.Default().AddTo( context );
        LeaseBuilder.For( license ).User( "alice" ).Machine( "desktop-1" ).AddTo( context );
        LeaseBuilder.For( license ).User( "ALICE" ).Machine( "laptop-1" ).AddTo( context );

        Assert.Equal( 1, context.Repository.GetActiveSeats( license.LicenseId, TestClock.Days( 1 ) ) );
    }

    [Fact]
    public async Task GetActiveSeats_HonoursMachinesPerUser()
    {
        await using LicenseServerTestContext context =
            await LicenseServerTestContext.CreateAsync( o => o.MachinesPerUser = 1 );

        License license = LicenseBuilder.Default().AddTo( context );
        LeaseBuilder.For( license ).User( "alice" ).Machine( "desktop-1" ).AddTo( context );
        LeaseBuilder.For( license ).User( "alice" ).Machine( "laptop-1" ).AddTo( context );

        // With one machine per seat, the same user on two machines consumes two seats.
        Assert.Equal( 2, context.Repository.GetActiveSeats( license.LicenseId, TestClock.Days( 1 ) ) );
    }

    /// <summary>
    /// A seat is counted from the machines a user works on, not from the leases they hold. A user can
    /// hold two leases on one machine, and charging them for a machine they do not have would deny a
    /// colleague a lease the license has the capacity for.
    /// </summary>
    /// <remarks>
    /// The lease service normally prevents a second lease on one machine by prolonging the first, but
    /// a server whose clock has moved backwards grants one. A load simulation produced exactly that
    /// within minutes of a restart.
    /// </remarks>
    [Fact]
    public async Task GetActiveSeats_TwoLeasesOnOneMachine_CountAsOneMachine()
    {
        await using LicenseServerTestContext context = await LicenseServerTestContext.CreateAsync();
        License license = LicenseBuilder.Default().AddTo( context );

        LeaseBuilder.For( license ).User( "alice" ).Machine( "desktop-1" ).AddTo( context );
        LeaseBuilder.For( license ).User( "alice" ).Machine( "desktop-1" ).AddTo( context );
        LeaseBuilder.For( license ).User( "alice" ).Machine( "laptop-1" ).AddTo( context );

        // Two machines at two machines per seat is one seat. Counting the three leases would make it
        // two.
        Assert.Equal( 1, context.Repository.GetActiveSeats( license.LicenseId, TestClock.Days( 1 ) ) );
    }
}
