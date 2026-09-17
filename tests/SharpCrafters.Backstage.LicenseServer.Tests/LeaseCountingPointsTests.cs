using SharpCrafters.Backstage.LicenseServer.Tests.Infrastructure;

namespace SharpCrafters.Backstage.LicenseServer.Tests;

/// <summary>
/// The usage timeline behind the graph: a sequence of lease open and close events, each carrying the
/// running seat count.
/// </summary>
public sealed class LeaseCountingPointsTests
{
    private static List<LeaseCountingPoint> Timeline( LicenseServerTestContext context, License license )
        => context.Repository
            .GetLeaseCountingPoints( license.LicenseId, TestClock.Days( -10 ), TestClock.Days( 100 ) )
            .ToList();

    [Fact]
    public async Task GetLeaseCountingPoints_OneLease_OpensThenCloses()
    {
        await using LicenseServerTestContext context = await LicenseServerTestContext.CreateAsync();
        License license = LicenseBuilder.Default().AddTo( context );
        LeaseBuilder.For( license ).From( TestClock.Origin ).Lasting( 3 ).AddTo( context );

        List<LeaseCountingPoint> points = Timeline( context, license );

        Assert.Equal( 2, points.Count );
        Assert.Equal( LeaseCountingPointKind.Open, points[0].Kind );
        Assert.Equal( 1, points[0].SeatCount );
        Assert.Equal( LeaseCountingPointKind.Close, points[1].Kind );
        Assert.Equal( 0, points[1].SeatCount );
    }

    [Fact]
    public async Task GetLeaseCountingPoints_ThreeUsersOneMachineEach_AreThreeSeats()
    {
        await using LicenseServerTestContext context = await LicenseServerTestContext.CreateAsync();
        License license = LicenseBuilder.Default().AddTo( context );

        foreach ( string user in new[] { "alice", "bob", "carol" } )
        {
            LeaseBuilder.For( license ).User( user ).Machine( $"desktop-{user}" )
                .From( TestClock.Origin ).Lasting( 3 ).AddTo( context );
        }

        List<LeaseCountingPoint> points = Timeline( context, license );

        Assert.Equal( 3, points.Max( p => p.SeatCount ) );
        Assert.Equal( 0, points[^1].SeatCount );
    }

    /// <summary>
    /// A user who gives up one machine and keeps another still holds their seat, and releases it only
    /// when the last of their leases ends.
    /// </summary>
    [Fact]
    public async Task GetLeaseCountingPoints_UserKeepingOneMachine_StaysCounted()
    {
        await using LicenseServerTestContext context = await LicenseServerTestContext.CreateAsync();
        License license = LicenseBuilder.Default().AddTo( context );

        LeaseBuilder.For( license ).User( "alice" ).Machine( "desktop-1" )
            .From( TestClock.Origin ).Lasting( 1 ).AddTo( context );

        LeaseBuilder.For( license ).User( "alice" ).Machine( "laptop-1" )
            .From( TestClock.Origin ).Lasting( 5 ).AddTo( context );

        List<LeaseCountingPoint> points = Timeline( context, license );

        // The first lease closes on day one and the second on day five. The seat is held throughout
        // and released only at the last point.
        Assert.All( points[..^1], p => Assert.Equal( 1, p.SeatCount ) );
        Assert.Equal( 0, points[^1].SeatCount );
    }

    [Fact]
    public async Task GetLeaseCountingPoints_OneUserTwoMachines_NeverExceedsOneSeat()
    {
        await using LicenseServerTestContext context = await LicenseServerTestContext.CreateAsync();
        License license = LicenseBuilder.Default().AddTo( context );
        LeaseBuilder.For( license ).User( "alice" ).Machine( "desktop-1" ).From( TestClock.Origin ).Lasting( 3 )
            .AddTo( context );

        LeaseBuilder.For( license ).User( "alice" ).Machine( "laptop-1" ).From( TestClock.Days( 1 ) ).Lasting( 3 )
            .AddTo( context );

        List<LeaseCountingPoint> points = Timeline( context, license );

        Assert.Equal( 1, points.Max( p => p.SeatCount ) );
    }

    [Fact]
    public async Task GetLeaseCountingPoints_OneUserThreeMachines_ReachesTwoSeats()
    {
        await using LicenseServerTestContext context = await LicenseServerTestContext.CreateAsync();
        License license = LicenseBuilder.Default().AddTo( context );

        foreach ( string machine in new[] { "desktop-1", "laptop-1", "desktop-2" } )
        {
            LeaseBuilder.For( license ).User( "alice" ).Machine( machine ).From( TestClock.Origin ).Lasting( 3 )
                .AddTo( context );
        }

        List<LeaseCountingPoint> points = Timeline( context, license );

        // One user on three machines is two seats: one seat covers two machines, and the third takes
        // a second seat.
        Assert.Equal( 2, points.Max( p => p.SeatCount ) );
    }

    /// <summary>
    /// When one lease ends at the exact instant another begins, the machine must be released before
    /// it is claimed again. This is what the numeric values of
    /// <see cref="LeaseCountingPointKind"/> encode, and it is the reason they must not be renumbered.
    /// </summary>
    [Fact]
    public async Task GetLeaseCountingPoints_CloseIsProcessedBeforeOpenAtTheSameInstant()
    {
        await using LicenseServerTestContext context =
            await LicenseServerTestContext.CreateAsync( o => o.MachinesPerUser = 1 );

        License license = LicenseBuilder.Default().AddTo( context );

        LeaseBuilder.For( license ).User( "alice" ).Machine( "desktop-1" )
            .From( TestClock.Origin ).To( TestClock.Days( 1 ) ).AddTo( context );

        LeaseBuilder.For( license ).User( "alice" ).Machine( "desktop-1" )
            .From( TestClock.Days( 1 ) ).To( TestClock.Days( 2 ) ).AddTo( context );

        List<LeaseCountingPoint> points = Timeline( context, license );

        // With one machine per seat, a transient double-count would show up as 2.
        Assert.Equal( 1, points.Max( p => p.SeatCount ) );

        LeaseCountingPoint[] atHandover = points.Where( p => p.Time == TestClock.Days( 1 ) ).ToArray();
        Assert.Equal( 2, atHandover.Length );
        Assert.Equal( LeaseCountingPointKind.Close, atHandover[0].Kind );
        Assert.Equal( LeaseCountingPointKind.Open, atHandover[1].Kind );
    }

    [Fact]
    public void LeaseCountingPointKind_OrdersCloseBeforeOpen()
    {
        // Pinned explicitly: the ordering of the timeline depends on these values.
        Assert.True( LeaseCountingPointKind.Close < LeaseCountingPointKind.Open );
    }

    [Fact]
    public async Task GetLeaseCountingPoints_IsOrderedByTime()
    {
        await using LicenseServerTestContext context = await LicenseServerTestContext.CreateAsync();
        License license = LicenseBuilder.Default().AddTo( context );

        for ( int i = 3; i >= 0; i-- )
        {
            LeaseBuilder.For( license ).User( $"user{i}" ).From( TestClock.Days( i ) ).Lasting( 1 ).AddTo( context );
        }

        List<LeaseCountingPoint> points = Timeline( context, license );

        Assert.Equal( points.Select( p => p.Time ).Order(), points.Select( p => p.Time ) );
    }

    [Fact]
    public async Task GetLeaseCountingPoints_IsDeterministic()
    {
        await using LicenseServerTestContext context = await LicenseServerTestContext.CreateAsync();
        License license = LicenseBuilder.Default().AddTo( context );

        // Several leases starting and ending at the same instants, so ties are everywhere.
        for ( int i = 0; i < 5; i++ )
        {
            LeaseBuilder.For( license ).User( $"user{i}" ).From( TestClock.Origin ).Lasting( 1 ).AddTo( context );
        }

        string First() => string.Join(
            "|",
            Timeline( context, license ).Select( p => $"{p.Time:O}/{p.Kind}/{p.Lease.LeaseId}/{p.SeatCount}" ) );

        Assert.Equal( First(), First() );
    }

    [Fact]
    public async Task GetLeaseCountingPoints_ExcludesReplacedLeases()
    {
        await using LicenseServerTestContext context = await LicenseServerTestContext.CreateAsync();
        License license = LicenseBuilder.Default().AddTo( context );
        Lease original = LeaseBuilder.For( license ).AddTo( context );

        context.Repository.CancelLease( original, "admin", TestClock.Days( 1 ) );
        await context.Repository.SaveChangesAsync();

        Assert.DoesNotContain( Timeline( context, license ), p => p.Lease.LeaseId == original.LeaseId );
    }

    [Fact]
    public async Task GetLeaseCountingPoints_ExcludesLeasesOutsideTheWindow()
    {
        await using LicenseServerTestContext context = await LicenseServerTestContext.CreateAsync();
        License license = LicenseBuilder.Default().AddTo( context );

        LeaseBuilder.For( license ).User( "past" ).From( TestClock.Days( -30 ) ).Lasting( 1 ).AddTo( context );
        LeaseBuilder.For( license ).User( "inside" ).From( TestClock.Origin ).Lasting( 1 ).AddTo( context );
        LeaseBuilder.For( license ).User( "future" ).From( TestClock.Days( 30 ) ).Lasting( 1 ).AddTo( context );

        List<LeaseCountingPoint> points = context.Repository
            .GetLeaseCountingPoints( license.LicenseId, TestClock.Days( -1 ), TestClock.Days( 1 ) )
            .ToList();

        Assert.All( points, p => Assert.Equal( "inside", p.Lease.UserName ) );
    }

    /// <summary>
    /// Two open leases held by one user on one machine occupy one seat, and the timeline still
    /// returns to zero once both have ended.
    /// </summary>
    /// <remarks>
    /// The lease service normally prevents this by reusing or prolonging a lease instead of granting
    /// a second one, and an earlier version of this test recorded the situation as data the timeline
    /// was entitled to refuse. It is not: a server whose clock moves backwards -- a restart with
    /// <c>TimeAcceleration</c> set, a correction from a time server, a restored snapshot -- grants a
    /// second lease while the first is still open, and a load simulation produced exactly that within
    /// minutes. The usage page answered with HTTP 500 for as long as the older lease ran.
    /// </remarks>
    [Fact]
    public async Task GetLeaseCountingPoints_TwoOpenLeasesOnOneMachine_CountAsOneSeat()
    {
        await using LicenseServerTestContext context = await LicenseServerTestContext.CreateAsync();
        License license = LicenseBuilder.Default().AddTo( context );

        LeaseBuilder.For( license ).User( "alice" ).Machine( "desktop-1" )
            .From( TestClock.Origin ).Lasting( 3 ).AddTo( context );

        LeaseBuilder.For( license ).User( "alice" ).Machine( "desktop-1" )
            .From( TestClock.Days( 1 ) ).Lasting( 3 ).AddTo( context );

        List<LeaseCountingPoint> points = Timeline( context, license );

        Assert.Equal( 4, points.Count );
        Assert.Equal( 1, points.Max( p => p.SeatCount ) );
        Assert.Equal( 0, points[^1].SeatCount );
    }

    [Fact]
    public async Task GetLeaseCountingPoints_ReturnsToZeroAfterEveryLeaseEnds()
    {
        await using LicenseServerTestContext context = await LicenseServerTestContext.CreateAsync();
        License license = LicenseBuilder.Default().AddTo( context );

        for ( int i = 0; i < 6; i++ )
        {
            LeaseBuilder.For( license ).User( $"user{i}" ).Machine( $"machine-{i}" )
                .From( TestClock.Days( i * 0.5 ) ).Lasting( 2 ).AddTo( context );
        }

        List<LeaseCountingPoint> points = Timeline( context, license );

        Assert.NotEmpty( points );
        Assert.Equal( 0, points[^1].SeatCount );
        Assert.All( points, p => Assert.True( p.SeatCount >= 0 ) );
    }
}
