// Copyright (c) SharpCrafters s.r.o. See the LICENSE.md file in the root directory of this repository root for details.

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
        await using var context = await LicenseServerTestContext.CreateAsync();
        var license = LicenseBuilder.Default().AddTo( context );

        Assert.Equal( 0, context.Repository.GetActiveSeats( license.LicenseId, TestClock.Days( 1 ) ) );
    }

    [Fact]
    public async Task GetActiveSeats_OneUserOneMachine_ReturnsOne()
    {
        await using var context = await LicenseServerTestContext.CreateAsync();
        var license = LicenseBuilder.Default().AddTo( context );
        LeaseBuilder.For( license ).User( "alice" ).Machine( "desktop-1" ).AddTo( context );

        Assert.Equal( 1, context.Repository.GetActiveSeats( license.LicenseId, TestClock.Days( 1 ) ) );
    }

    [Fact]
    public async Task GetActiveSeats_OneUserTwoMachines_StillReturnsOne()
    {
        await using var context = await LicenseServerTestContext.CreateAsync();
        var license = LicenseBuilder.Default().AddTo( context );
        LeaseBuilder.For( license ).User( "alice" ).Machine( "desktop-1" ).AddTo( context );
        LeaseBuilder.For( license ).User( "alice" ).Machine( "laptop-1" ).AddTo( context );

        Assert.Equal( 1, context.Repository.GetActiveSeats( license.LicenseId, TestClock.Days( 1 ) ) );
    }

    [Fact]
    public async Task GetActiveSeats_OneUserThreeMachines_ReturnsTwo()
    {
        await using var context = await LicenseServerTestContext.CreateAsync();
        var license = LicenseBuilder.Default().AddTo( context );

        foreach ( var machine in new[] { "desktop-1", "laptop-1", "desktop-2" } )
        {
            LeaseBuilder.For( license ).User( "alice" ).Machine( machine ).AddTo( context );
        }

        Assert.Equal( 2, context.Repository.GetActiveSeats( license.LicenseId, TestClock.Days( 1 ) ) );
    }

    [Fact]
    public async Task GetActiveSeats_TwoUsers_ReturnsTwo()
    {
        await using var context = await LicenseServerTestContext.CreateAsync();
        var license = LicenseBuilder.Default().AddTo( context );
        LeaseBuilder.For( license ).User( "alice" ).AddTo( context );
        LeaseBuilder.For( license ).User( "bob" ).AddTo( context );

        Assert.Equal( 2, context.Repository.GetActiveSeats( license.LicenseId, TestClock.Days( 1 ) ) );
    }

    [Fact]
    public async Task GetActiveSeats_LeaseStartingExactlyNow_IsCounted()
    {
        await using var context = await LicenseServerTestContext.CreateAsync();
        var license = LicenseBuilder.Default().AddTo( context );
        LeaseBuilder.For( license ).From( TestClock.Origin ).Lasting( 3 ).AddTo( context );

        Assert.Equal( 1, context.Repository.GetActiveSeats( license.LicenseId, TestClock.Origin ) );
    }

    [Fact]
    public async Task GetActiveSeats_LeaseEndingExactlyNow_IsNotCounted()
    {
        await using var context = await LicenseServerTestContext.CreateAsync();
        var license = LicenseBuilder.Default().AddTo( context );
        LeaseBuilder.For( license ).From( TestClock.Origin ).Lasting( 3 ).AddTo( context );

        Assert.Equal( 0, context.Repository.GetActiveSeats( license.LicenseId, TestClock.Days( 3 ) ) );
    }

    [Fact]
    public async Task GetActiveSeats_OtherLicense_IsNotCounted()
    {
        await using var context = await LicenseServerTestContext.CreateAsync();
        var first = LicenseBuilder.Default().WithLicenseId( 1 ).AddTo( context );
        var second = LicenseBuilder.Default().WithLicenseId( 2 ).AddTo( context );

        LeaseBuilder.For( second ).AddTo( context );

        Assert.Equal( 0, context.Repository.GetActiveSeats( first.LicenseId, TestClock.Days( 1 ) ) );
        Assert.Equal( 1, context.Repository.GetActiveSeats( second.LicenseId, TestClock.Days( 1 ) ) );
    }

    [Fact]
    public async Task GetActiveSeats_ReplacedLease_IsNotCounted()
    {
        await using var context = await LicenseServerTestContext.CreateAsync();
        var license = LicenseBuilder.Default().AddTo( context );
        var original = LeaseBuilder.For( license ).AddTo( context );

        context.Repository.CancelLease( original, "admin", TestClock.Days( 1 ) );
        await context.Repository.SaveChangesAsync();

        Assert.Equal( 0, context.Repository.GetActiveSeats( license.LicenseId, TestClock.Days( 2 ) ) );
    }

    /// <summary>
    /// The default collation of SQL Server ignores the case. The database held in memory uses the
    /// same collation, so that a test cannot pass here and fail in production.
    /// </summary>
    [Fact]
    public async Task GetActiveSeats_UserNameCasingDiffers_CountsAsOneUser()
    {
        await using var context = await LicenseServerTestContext.CreateAsync();
        var license = LicenseBuilder.Default().AddTo( context );
        LeaseBuilder.For( license ).User( "alice" ).Machine( "desktop-1" ).AddTo( context );
        LeaseBuilder.For( license ).User( "ALICE" ).Machine( "laptop-1" ).AddTo( context );

        Assert.Equal( 1, context.Repository.GetActiveSeats( license.LicenseId, TestClock.Days( 1 ) ) );
    }

    [Fact]
    public async Task GetActiveSeats_HonoursMachinesPerUser()
    {
        await using var context =
            await LicenseServerTestContext.CreateAsync( o => o.MachinesPerUser = 1 );

        var license = LicenseBuilder.Default().AddTo( context );
        LeaseBuilder.For( license ).User( "alice" ).Machine( "desktop-1" ).AddTo( context );
        LeaseBuilder.For( license ).User( "alice" ).Machine( "laptop-1" ).AddTo( context );

        // With one machine per seat, the same user on two machines consumes two seats.
        Assert.Equal( 2, context.Repository.GetActiveSeats( license.LicenseId, TestClock.Days( 1 ) ) );
    }

    /// <summary>
    /// A seat is counted from the machines a user works on, and not from the leases that user holds.
    /// A user can hold two leases on one machine. Counting a second machine for that user would deny
    /// a lease to a colleague, while the license still has a free seat.
    /// </summary>
    /// <remarks>
    /// The lease service prevents a second lease on one machine: it prolongs the first lease. A
    /// server whose clock moved backwards grants a second lease. A load simulation produced this
    /// state a few minutes after a restart.
    /// </remarks>
    [Fact]
    public async Task GetActiveSeats_TwoLeasesOnOneMachine_CountAsOneMachine()
    {
        await using var context = await LicenseServerTestContext.CreateAsync();
        var license = LicenseBuilder.Default().AddTo( context );

        LeaseBuilder.For( license ).User( "alice" ).Machine( "desktop-1" ).AddTo( context );
        LeaseBuilder.For( license ).User( "alice" ).Machine( "desktop-1" ).AddTo( context );
        LeaseBuilder.For( license ).User( "alice" ).Machine( "laptop-1" ).AddTo( context );

        // Two machines at two machines per seat is one seat. Counting the three leases would make it
        // two.
        Assert.Equal( 1, context.Repository.GetActiveSeats( license.LicenseId, TestClock.Days( 1 ) ) );
    }
}