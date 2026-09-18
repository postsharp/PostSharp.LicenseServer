// Copyright (c) SharpCrafters s.r.o. See the LICENSE.md file in the root directory of this repository root for details.

using SharpCrafters.Backstage.LicenseServer.Data;
using SharpCrafters.Backstage.LicenseServer.Services;
using SharpCrafters.Backstage.LicenseServer.Tests.Infrastructure;

namespace SharpCrafters.Backstage.LicenseServer.Tests;

/// <summary>
/// Whether the server can serve a lease, which is what the health check reports.
/// </summary>
/// <remarks>
/// The last tests of this class ask the allocator the same question, by allocating a lease. This
/// service applies the rules of <see cref="LeaseService"/> without allocating, and the two
/// implementations must give the same answer.
/// </remarks>
public sealed class LicenseAvailabilityTests
{
    private static readonly DateTime now = TestClock.Days( 1 );

    private static LicenseAvailabilityService CreateService( LicenseServerTestContext context )
        => new( context.Repository, context.LicenseParser, context.ServerVersion );

    private static async Task<LicenseAvailability> GetAvailabilityAsync( LicenseServerTestContext context )
        => await CreateService( context ).GetAvailabilityAsync( now );

    /// <summary>
    /// Fills a license to the number of seats given, one machine per user.
    /// </summary>
    private static void Occupy( LicenseServerTestContext context, License license, int seats )
    {
        for ( var i = 0; i < seats; i++ )
        {
            LeaseBuilder.For( license ).User( $"user{i}" ).Machine( $"machine{i}" ).AddTo( context );
        }
    }

    [Fact]
    public async Task Availability_NoLicense_CannotServe()
    {
        await using var context = await LicenseServerTestContext.CreateAsync();

        var availability = await GetAvailabilityAsync( context );

        Assert.False( availability.CanServeLease );
        Assert.Equal( 0, availability.Total );
        Assert.Equal( "No license is registered.", availability.Describe() );
    }

    [Fact]
    public async Task Availability_FreeCapacity_CanServe()
    {
        await using var context = await LicenseServerTestContext.CreateAsync();
        var license = LicenseBuilder.Default().WithUsers( 5 ).AddTo( context );
        Occupy( context, license, 3 );

        var availability = await GetAvailabilityAsync( context );

        Assert.True( availability.CanServeLease );
        Assert.Equal( 1, availability.Available );
    }

    [Fact]
    public async Task Availability_NoSeatLimit_CanServe()
    {
        await using var context = await LicenseServerTestContext.CreateAsync();
        LicenseBuilder.Default().WithUsers( null ).AddTo( context );

        Assert.True( ( await GetAvailabilityAsync( context ) ).CanServeLease );
    }

    [Fact]
    public async Task Availability_Disabled_CannotServe()
    {
        await using var context = await LicenseServerTestContext.CreateAsync();
        LicenseBuilder.Default().WithUsers( 5 ).WithPriority( -1 ).AddTo( context );

        var availability = await GetAvailabilityAsync( context );

        Assert.False( availability.CanServeLease );
        Assert.Equal( 1, availability.Disabled );
        Assert.Contains( "1 disabled", availability.Describe(), StringComparison.Ordinal );
    }

    [Fact]
    public async Task Availability_Expired_CannotServe()
    {
        await using var context = await LicenseServerTestContext.CreateAsync();
        LicenseBuilder.Default().WithUsers( 5 ).WithValidTo( TestClock.Days( 0.5 ) ).AddTo( context );

        var availability = await GetAvailabilityAsync( context );

        Assert.False( availability.CanServeLease );
        Assert.Equal( 1, availability.Expired );
    }

    /// <summary>
    /// A license key that the server cannot parse counts as invalid. The home page reports the same
    /// state.
    /// </summary>
    [Fact]
    public async Task Availability_UnparsableKey_CannotServe()
    {
        await using var context = await LicenseServerTestContext.CreateAsync();

        context.Db.Licenses.Add( new License { LicenseId = 1, LicenseKey = "NOT-A-KEY", ProductCode = "Ultimate", CreatedOn = TestClock.Origin } );

        await context.Db.SaveChangesAsync();

        var availability = await GetAvailabilityAsync( context );

        Assert.False( availability.CanServeLease );
        Assert.Equal( 1, availability.Invalid );
    }

    /// <summary>
    /// The license is full, and its grace period still has a free seat and remaining days, so the
    /// server serves the next request.
    /// </summary>
    [Fact]
    public async Task Availability_FullWithGraceLeft_CanServe()
    {
        await using var context = await LicenseServerTestContext.CreateAsync();

        // Five seats plus 20 per cent is a grace limit of six.
        var license = LicenseBuilder.Default().WithUsers( 5 ).WithGracePercent( 20 ).AddTo( context );
        Occupy( context, license, 5 );

        Assert.True( ( await GetAvailabilityAsync( context ) ).CanServeLease );
    }

    [Fact]
    public async Task Availability_GraceSeatsUsedUp_CannotServe()
    {
        await using var context = await LicenseServerTestContext.CreateAsync();
        var license = LicenseBuilder.Default().WithUsers( 5 ).WithGracePercent( 20 ).AddTo( context );
        Occupy( context, license, 6 );

        var availability = await GetAvailabilityAsync( context );

        Assert.False( availability.CanServeLease );
        Assert.Equal( 1, availability.Exhausted );
        Assert.Contains( "1 at capacity", availability.Describe(), StringComparison.Ordinal );
    }

    [Fact]
    public async Task Availability_GracePeriodOver_CannotServe()
    {
        await using var context = await LicenseServerTestContext.CreateAsync();

        var license = LicenseBuilder.Default()
            .WithUsers( 5 )
            .WithGraceDays( 10 )
            .WithGraceStartTime( TestClock.Days( -20 ) )
            .AddTo( context );

        Occupy( context, license, 5 );

        var availability = await GetAvailabilityAsync( context );

        Assert.False( availability.CanServeLease );
        Assert.Equal( 1, availability.Exhausted );
    }

    /// <summary>
    /// Reading the availability does not start the grace period of a license. The allocator starts
    /// that period when it grants a lease within it. A probe that did the same would let the period
    /// elapse while the server is idle.
    /// </summary>
    [Fact]
    public async Task Availability_FullLicense_DoesNotStartTheGracePeriod()
    {
        await using var context = await LicenseServerTestContext.CreateAsync();
        var license = LicenseBuilder.Default().WithUsers( 5 ).AddTo( context );
        Occupy( context, license, 5 );

        await GetAvailabilityAsync( context );

        Assert.Null( context.Db.Licenses.Single().GraceStartTime );
    }

    /// <summary>
    /// The health check and the allocator answer the same question. A server that the check reports
    /// as available grants a lease, and a server that it reports as unavailable denies the request.
    /// </summary>
    [Theory]
    [InlineData( 3, false, true )]
    [InlineData( 5, false, true )]
    [InlineData( 6, false, false )]
    [InlineData( 5, true, false )]
    public async Task Availability_AgreesWithTheAllocator( int seatsInUse, bool graceOver, bool expected )
    {
        await using var context = await LicenseServerTestContext.CreateAsync();

        var builder = LicenseBuilder.Default().WithUsers( 5 ).WithGracePercent( 20 ).WithGraceDays( 10 );

        if ( graceOver )
        {
            builder = builder.WithGraceStartTime( TestClock.Days( -20 ) );
        }

        var license = builder.AddTo( context );
        Occupy( context, license, seatsInUse );

        Assert.Equal( expected, ( await GetAvailabilityAsync( context ) ).CanServeLease );

        // Asked afterwards, because allocating changes the state the check reads.
        var granted = await context.LeaseService.GetLicenseLeaseAsync(
            null,
            new Version( 2027, 0 ),
            null,
            "new-machine",
            "new-user",
            "new-user",
            now,
            [] );

        Assert.Equal( expected, granted != null );
    }
}