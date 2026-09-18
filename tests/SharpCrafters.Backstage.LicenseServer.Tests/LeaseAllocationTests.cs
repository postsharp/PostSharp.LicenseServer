// Copyright (c) SharpCrafters s.r.o. See the LICENSE.md file in the root directory of this repository root for details.

using Microsoft.EntityFrameworkCore;
using SharpCrafters.Backstage.LicenseServer.Tests.Infrastructure;

namespace SharpCrafters.Backstage.LicenseServer.Tests;

/// <summary>
/// The rules that decide whether a developer receives a license. The server reuses the lease the
/// developer holds, then grants a free seat, then grants a seat of the grace period, and denies the
/// request when none of the three applies.
/// </summary>
public sealed class LeaseAllocationTests
{
    private static readonly Version currentVersion = new( 2025, 1, 0 );

    private static Task<Lease?> RequestAsync(
        LicenseServerTestContext context,
        License[] licenses,
        string user = "alice",
        string machine = "desktop-1",
        DateTime? now = null,
        Dictionary<int, string>? errors = null )
        => context.LeaseService.GetLeaseAsync(
            currentVersion,
            null,
            machine,
            user,
            user,
            now ?? TestClock.Origin,
            errors ?? [],
            licenses );

    [Fact]
    public async Task GetLease_NoExistingLease_GrantsANewOne()
    {
        await using var context = await LicenseServerTestContext.CreateAsync();
        var license = LicenseBuilder.Default().WithUsers( 5 ).AddTo( context );

        var lease = await RequestAsync( context, [license] );

        Assert.NotNull( lease );
        Assert.Equal( "alice", lease.UserName );
        Assert.Equal( "desktop-1", lease.Machine );
        Assert.Equal( TestClock.Origin, lease.StartTime );
        Assert.Equal( TestClock.Days( 3 ), lease.EndTime );
        Assert.False( lease.Grace );
    }

    [Fact]
    public async Task GetLease_GoodExistingLease_IsReusedWithoutInsertingARow()
    {
        await using var context = await LicenseServerTestContext.CreateAsync();
        var license = LicenseBuilder.Default().AddTo( context );
        var existing = LeaseBuilder.For( license ).From( TestClock.Origin ).Lasting( 3 ).AddTo( context );

        var lease = await RequestAsync( context, [license], now: TestClock.Days( 1 ) );

        Assert.Equal( existing.LeaseId, lease!.LeaseId );
        Assert.DoesNotContain( context.Db.ChangeTracker.Entries<Lease>(), e => e.State == EntityState.Added );
    }

    [Fact]
    public async Task GetLease_LeaseNearingExpiry_IsProlonged()
    {
        await using var context = await LicenseServerTestContext.CreateAsync();
        var license = LicenseBuilder.Default().AddTo( context );
        var existing = LeaseBuilder.For( license ).From( TestClock.Origin ).Lasting( 3 ).AddTo( context );

        // Within MinLeaseDays of the end, so the client is told to renew.
        var lease = await RequestAsync( context, [license], now: TestClock.Days( 2.5 ) );

        Assert.NotNull( lease );
        Assert.NotEqual( existing.LeaseId, lease.LeaseId );
        Assert.Equal( existing.LeaseId, lease.OverwrittenLeaseId );
        Assert.Equal( existing.StartTime, lease.StartTime );
        Assert.Equal( TestClock.Days( 5.5 ), lease.EndTime );
    }

    [Fact]
    public async Task GetLease_SecondMachineForTheSameUser_DoesNotConsumeASeat()
    {
        await using var context = await LicenseServerTestContext.CreateAsync();
        var license = LicenseBuilder.Default().WithUsers( 1 ).AddTo( context );
        LeaseBuilder.For( license ).User( "alice" ).Machine( "desktop-1" ).AddTo( context );

        var lease = await RequestAsync( context, [license], machine: "laptop-1", now: TestClock.Days( 1 ) );
        await context.Repository.SaveChangesAsync();

        Assert.NotNull( lease );
        Assert.Equal( 1, context.Repository.GetActiveSeats( license.LicenseId, TestClock.Days( 1 ) ) );
    }

    [Fact]
    public async Task GetLease_ThirdMachineOnAFullLicense_FallsBackToGrace()
    {
        await using var context = await LicenseServerTestContext.CreateAsync();
        var license = LicenseBuilder.Default().WithUsers( 1 ).AddTo( context );
        LeaseBuilder.For( license ).User( "alice" ).Machine( "desktop-1" ).AddTo( context );
        LeaseBuilder.For( license ).User( "alice" ).Machine( "laptop-1" ).AddTo( context );

        // A third machine rounds up to a second seat, which this one-seat license does not have.
        var lease = await RequestAsync( context, [license], machine: "desktop-2", now: TestClock.Days( 1 ) );

        Assert.NotNull( lease );
        Assert.True( lease.Grace );
    }

    [Fact]
    public async Task GetLease_CapacityAvailable_DoesNotUseGrace()
    {
        await using var context = await LicenseServerTestContext.CreateAsync();
        var license = LicenseBuilder.Default().WithUsers( 5 ).AddTo( context );
        LeaseBuilder.For( license ).User( "bob" ).AddTo( context );

        var lease = await RequestAsync( context, [license], now: TestClock.Days( 1 ) );

        Assert.False( lease!.Grace );
        Assert.Null( license.GraceStartTime );
    }

    [Fact]
    public async Task GetLease_CapacityExhausted_StartsTheGracePeriodAndWarns()
    {
        await using var context = await LicenseServerTestContext.CreateAsync();
        var license = LicenseBuilder.Default().WithUsers( 1 ).AddTo( context );
        LeaseBuilder.For( license ).User( "bob" ).AddTo( context );

        var lease = await RequestAsync( context, [license], now: TestClock.Days( 1 ) );

        Assert.NotNull( lease );
        Assert.True( lease.Grace );
        Assert.Equal( TestClock.Days( 1 ), license.GraceStartTime );

        var warning = Assert.Single( context.EmailSender.WithSubject( "WARNING" ) );
        Assert.Equal( "admin@example.com", warning.To );
        Assert.Contains( "capacity of 1 concurrent user", warning.Body, StringComparison.Ordinal );
    }

    [Fact]
    public async Task GetLease_WithinGraceLimit_IsGranted()
    {
        await using var context = await LicenseServerTestContext.CreateAsync();

        // 10 seats, 20 percent grace, so up to 12 are tolerated.
        var license = LicenseBuilder.Default()
            .WithUsers( 10 )
            .WithGracePercent( 20 )
            .WithGraceStartTime( TestClock.Origin )
            .AddTo( context );

        for ( var i = 0; i < 11; i++ )
        {
            LeaseBuilder.For( license ).User( $"user{i}" ).AddTo( context );
        }

        var lease = await RequestAsync( context, [license], user: "newcomer", now: TestClock.Days( 1 ) );

        Assert.NotNull( lease );
        Assert.True( lease.Grace );
    }

    [Fact]
    public async Task GetLease_AtTheGraceLimit_IsDenied()
    {
        await using var context = await LicenseServerTestContext.CreateAsync();

        var license = LicenseBuilder.Default()
            .WithUsers( 10 )
            .WithGracePercent( 20 )
            .WithGraceStartTime( TestClock.Origin )
            .AddTo( context );

        for ( var i = 0; i < 12; i++ )
        {
            LeaseBuilder.For( license ).User( $"user{i}" ).AddTo( context );
        }

        var lease = await RequestAsync( context, [license], user: "newcomer", now: TestClock.Days( 1 ) );

        Assert.Null( lease );
    }

    [Fact]
    public async Task GetLease_GracePeriodOver_IsDenied()
    {
        await using var context = await LicenseServerTestContext.CreateAsync();

        var license = LicenseBuilder.Default()
            .WithUsers( 1 )
            .WithGraceDays( 30 )
            .WithGraceStartTime( TestClock.Origin )
            .AddTo( context );

        LeaseBuilder.For( license ).User( "bob" ).From( TestClock.Days( 40 ) ).Lasting( 3 ).AddTo( context );

        var lease = await RequestAsync( context, [license], now: TestClock.Days( 41 ) );

        Assert.Null( lease );
    }

    [Fact]
    public async Task GetLease_GraceLease_EndsWhenTheGracePeriodEnds()
    {
        await using var context = await LicenseServerTestContext.CreateAsync();

        var license = LicenseBuilder.Default()
            .WithUsers( 1 )
            .WithGraceDays( 30 )
            .WithGraceStartTime( TestClock.Origin )
            .AddTo( context );

        LeaseBuilder.For( license ).User( "bob" ).From( TestClock.Days( 28 ) ).Lasting( 5 ).AddTo( context );

        var lease = await RequestAsync( context, [license], now: TestClock.Days( 29 ) );

        // A three-day lease would run past the end of the grace period, so it is cut short.
        Assert.NotNull( lease );
        Assert.Equal( TestClock.Days( 30 ), lease.EndTime );
    }

    [Fact]
    public async Task GetLease_DeniedRequest_NotifiesTheAdministrator()
    {
        await using var context = await LicenseServerTestContext.CreateAsync();

        var license = LicenseBuilder.Default()
            .WithUsers( 1 )
            .WithGracePercent( 0 )
            .WithGraceStartTime( TestClock.Origin.AddDays( -100 ) )
            .WithGraceDays( 1 )
            .AddTo( context );

        LeaseBuilder.For( license ).User( "bob" ).AddTo( context );

        var lease = await RequestAsync( context, [license], now: TestClock.Days( 1 ) );

        Assert.Null( lease );
        var denial = Assert.Single( context.EmailSender.WithSubject( "denied" ) );
        Assert.Contains( "alice", denial.Body, StringComparison.Ordinal );
        Assert.Contains( "desktop-1", denial.Body, StringComparison.Ordinal );
    }

    [Fact]
    public async Task GetLease_LeaseEndClampedByLicenseExpiry()
    {
        await using var context = await LicenseServerTestContext.CreateAsync();
        var license = LicenseBuilder.Default().WithValidTo( TestClock.Days( 1 ) ).AddTo( context );

        var lease = await RequestAsync( context, [license] );

        Assert.NotNull( lease );
        Assert.Equal( TestClock.Days( 1 ), lease.EndTime );
    }

    [Fact]
    public async Task GetLease_LicenseAlreadyExpired_IsDenied()
    {
        await using var context = await LicenseServerTestContext.CreateAsync();
        var license = LicenseBuilder.Default().WithValidTo( TestClock.Days( -1 ) ).AddTo( context );

        Assert.Null( await RequestAsync( context, [license] ) );
    }

    [Fact]
    public async Task GetLease_TriesLicensesInPriorityOrder()
    {
        await using var context = await LicenseServerTestContext.CreateAsync();
        var low = LicenseBuilder.Default().WithLicenseId( 1 ).WithPriority( 10 ).AddTo( context );
        var high = LicenseBuilder.Default().WithLicenseId( 2 ).WithPriority( 0 ).AddTo( context );

        var lease = await RequestAsync( context, [high, low] );

        Assert.Equal( high.LicenseId, lease!.LicenseId );
    }

    /// <summary>
    /// The server records a warning even when it could not send it, so that an SMTP server that
    /// fails does not turn every later request into another attempt.
    /// </summary>
    [Fact]
    public async Task GetLease_WarningEmailFails_StillRecordsThatItWasAttempted()
    {
        await using var context = await LicenseServerTestContext.CreateAsync();
        context.EmailSender.ThrowOnSend = new InvalidOperationException( "SMTP is down" );

        var license = LicenseBuilder.Default().WithUsers( 1 ).AddTo( context );
        LeaseBuilder.For( license ).User( "bob" ).AddTo( context );

        var lease = await RequestAsync( context, [license], now: TestClock.Days( 1 ) );

        Assert.NotNull( lease );
        Assert.Equal( TestClock.Days( 1 ), license.GraceLastWarningTime );
    }

    [Fact]
    public async Task GetLease_WarningIsNotRepeatedWithinTheConfiguredInterval()
    {
        await using var context =
            await LicenseServerTestContext.CreateAsync( o => o.GracePeriodWarningDays = 7 );

        var license = LicenseBuilder.Default().WithUsers( 1 ).AddTo( context );
        LeaseBuilder.For( license ).User( "bob" ).AddTo( context );

        await RequestAsync( context, [license], user: "alice", now: TestClock.Days( 1 ) );
        await context.Repository.SaveChangesAsync();
        await RequestAsync( context, [license], user: "carol", now: TestClock.Days( 2 ) );

        Assert.Single( context.EmailSender.WithSubject( "WARNING" ) );
    }
}