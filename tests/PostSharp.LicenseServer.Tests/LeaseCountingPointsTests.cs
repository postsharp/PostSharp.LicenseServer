using PostSharp.LicenseServer.Tests.Infrastructure;

namespace PostSharp.LicenseServer.Tests;

/// <summary>
/// The usage timeline behind the graph: a sequence of lease open and close events carrying the
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
        Assert.Equal( 1, points[0].LeaseCount );
        Assert.Equal( LeaseCountingPointKind.Close, points[1].Kind );
        Assert.Equal( 0, points[1].LeaseCount );
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

        Assert.Equal( 1, Timeline( context, license ).Max( p => p.LeaseCount ) );
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

        Assert.Equal( 2, Timeline( context, license ).Max( p => p.LeaseCount ) );
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
        Assert.Equal( 1, points.Max( p => p.LeaseCount ) );

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
            Timeline( context, license ).Select( p => $"{p.Time:O}/{p.Kind}/{p.Lease.LeaseId}/{p.LeaseCount}" ) );

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
    /// The timeline assumes a user never holds two open leases on the same machine at once, which
    /// is an invariant the lease service maintains by reusing or prolonging a lease instead of
    /// granting a second one. Data that breaks it is reported rather than silently miscounted,
    /// because an under-count in a licensing audit is worse than a failure.
    /// </summary>
    [Fact]
    public async Task GetLeaseCountingPoints_OverlappingLeasesOnOneMachine_AreReported()
    {
        await using LicenseServerTestContext context = await LicenseServerTestContext.CreateAsync();
        License license = LicenseBuilder.Default().AddTo( context );

        LeaseBuilder.For( license ).User( "alice" ).Machine( "desktop-1" )
            .From( TestClock.Origin ).Lasting( 3 ).AddTo( context );

        LeaseBuilder.For( license ).User( "alice" ).Machine( "desktop-1" )
            .From( TestClock.Days( 1 ) ).Lasting( 3 ).AddTo( context );

        InvalidOperationException exception =
            Assert.Throws<InvalidOperationException>( () => Timeline( context, license ) );

        Assert.Contains( "which is not open", exception.Message, StringComparison.Ordinal );
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
        Assert.Equal( 0, points[^1].LeaseCount );
        Assert.All( points, p => Assert.True( p.LeaseCount >= 0 ) );
    }
}
