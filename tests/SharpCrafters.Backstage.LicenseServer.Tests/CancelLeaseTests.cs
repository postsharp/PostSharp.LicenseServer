using Microsoft.EntityFrameworkCore;
using PostSharp.LicenseServer.Tests.Infrastructure;

namespace PostSharp.LicenseServer.Tests;

/// <summary>
/// An administrator can end a lease early, which inserts a replacement ending now rather than
/// deleting anything.
/// </summary>
public sealed class CancelLeaseTests
{
    [Fact]
    public async Task CancelLease_InsertsAReplacementEndingNow()
    {
        await using LicenseServerTestContext context = await LicenseServerTestContext.CreateAsync();
        License license = LicenseBuilder.Default().AddTo( context );
        Lease original = LeaseBuilder.For( license ).From( TestClock.Origin ).Lasting( 3 ).AddTo( context );

        context.Repository.CancelLease( original, "DOMAIN\\admin", TestClock.Days( 1 ) );
        await context.Repository.SaveChangesAsync();

        Lease replacement = await context.Db.Leases.SingleAsync( l => l.LeaseId != original.LeaseId );

        Assert.Equal( original.LeaseId, replacement.OverwrittenLeaseId );
        Assert.Equal( TestClock.Days( 1 ), replacement.EndTime );
        Assert.Equal( original.StartTime, replacement.StartTime );
        Assert.Equal( original.UserName, replacement.UserName );
        Assert.Equal( original.Machine, replacement.Machine );
        Assert.Equal( "DOMAIN\\admin", replacement.AuthenticatedUser );
    }

    [Fact]
    public async Task CancelLease_KeepsTheOriginalRow()
    {
        await using LicenseServerTestContext context = await LicenseServerTestContext.CreateAsync();
        License license = LicenseBuilder.Default().AddTo( context );
        Lease original = LeaseBuilder.For( license ).AddTo( context );

        context.Repository.CancelLease( original, "admin", TestClock.Days( 1 ) );
        await context.Repository.SaveChangesAsync();

        // The audit log is append-only.
        Assert.Equal( 2, await context.Db.Leases.CountAsync() );
        Assert.NotNull( await context.Db.Leases.FindAsync( original.LeaseId ) );
    }

    [Fact]
    public async Task CancelLease_ReleasesTheSeat()
    {
        await using LicenseServerTestContext context = await LicenseServerTestContext.CreateAsync();
        License license = LicenseBuilder.Default().AddTo( context );
        Lease original = LeaseBuilder.For( license ).From( TestClock.Origin ).Lasting( 3 ).AddTo( context );

        Assert.Equal( 1, context.Repository.GetActiveLeads( license.LicenseId, TestClock.Days( 2 ) ) );

        context.Repository.CancelLease( original, "admin", TestClock.Days( 1 ) );
        await context.Repository.SaveChangesAsync();

        Assert.Equal( 0, context.Repository.GetActiveLeads( license.LicenseId, TestClock.Days( 2 ) ) );
    }

    /// <summary>
    /// Cancelling skips the end-time adjustment applied to new leases. Without that, a lease ending
    /// "now" would be rejected for not extending beyond the current moment, and cancelling would
    /// silently do nothing.
    /// </summary>
    [Fact]
    public async Task CancelLease_IsNotRejectedForEndingImmediately()
    {
        await using LicenseServerTestContext context = await LicenseServerTestContext.CreateAsync();
        License license = LicenseBuilder.Default().WithValidTo( TestClock.Days( 2 ) ).AddTo( context );
        Lease original = LeaseBuilder.For( license ).From( TestClock.Origin ).Lasting( 2 ).AddTo( context );

        context.Repository.CancelLease( original, "admin", TestClock.Days( 1 ) );
        await context.Repository.SaveChangesAsync();

        Lease replacement = await context.Db.Leases.SingleAsync( l => l.LeaseId != original.LeaseId );
        Assert.Equal( TestClock.Days( 1 ), replacement.EndTime );
    }

    [Fact]
    public async Task CancelLease_SignsTheReplacement()
    {
        await using LicenseServerTestContext context = await LicenseServerTestContext.CreateAsync();
        License license = LicenseBuilder.Default().AddTo( context );
        Lease original = LeaseBuilder.For( license ).AddTo( context );

        context.Repository.CancelLease( original, "admin", TestClock.Days( 1 ) );
        await context.Repository.SaveChangesAsync();

        Lease replacement = await context.Db.Leases.SingleAsync( l => l.LeaseId != original.LeaseId );
        Assert.False( string.IsNullOrEmpty( replacement.HMAC ) );
    }

    [Fact]
    public async Task CancelLease_ThenRequestAgain_GrantsAFreshLease()
    {
        await using LicenseServerTestContext context = await LicenseServerTestContext.CreateAsync();
        License license = LicenseBuilder.Default().AddTo( context );
        Lease original = LeaseBuilder.For( license ).User( "alice" ).Machine( "desktop-1" ).AddTo( context );

        context.Repository.CancelLease( original, "admin", TestClock.Days( 1 ) );
        await context.Repository.SaveChangesAsync();

        Lease? lease = await context.LeaseService.GetLeaseAsync(
            new Version( 2025, 1, 0 ),
            null,
            "desktop-1",
            "alice",
            "alice",
            TestClock.Days( 2 ),
            [],
            [license] );

        Assert.NotNull( lease );
        Assert.NotEqual( original.LeaseId, lease.LeaseId );
        Assert.Equal( TestClock.Days( 2 ), lease.StartTime );
    }
}
