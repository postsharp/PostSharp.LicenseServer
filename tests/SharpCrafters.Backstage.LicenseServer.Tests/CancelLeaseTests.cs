// Copyright (c) SharpCrafters s.r.o. See the LICENSE.md file in the root directory of this repository root for details.

using Microsoft.EntityFrameworkCore;
using SharpCrafters.Backstage.LicenseServer.Tests.Infrastructure;

namespace SharpCrafters.Backstage.LicenseServer.Tests;

/// <summary>
/// An administrator can end a lease before its end time. The server inserts a replacement that ends
/// at the current instant, and it deletes nothing.
/// </summary>
public sealed class CancelLeaseTests
{
    [Fact]
    public async Task CancelLease_InsertsAReplacementEndingNow()
    {
        await using var context = await LicenseServerTestContext.CreateAsync();
        var license = LicenseBuilder.Default().AddTo( context );
        var original = LeaseBuilder.For( license ).From( TestClock.Origin ).Lasting( 3 ).AddTo( context );

        context.Repository.CancelLease( original, "DOMAIN\\admin", TestClock.Days( 1 ) );
        await context.Repository.SaveChangesAsync();

        var replacement = await context.Db.Leases.SingleAsync( l => l.LeaseId != original.LeaseId );

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
        await using var context = await LicenseServerTestContext.CreateAsync();
        var license = LicenseBuilder.Default().AddTo( context );
        var original = LeaseBuilder.For( license ).AddTo( context );

        context.Repository.CancelLease( original, "admin", TestClock.Days( 1 ) );
        await context.Repository.SaveChangesAsync();

        // The audit log is append-only.
        Assert.Equal( 2, await context.Db.Leases.CountAsync() );
        Assert.NotNull( await context.Db.Leases.FindAsync( original.LeaseId ) );
    }

    [Fact]
    public async Task CancelLease_ReleasesTheSeat()
    {
        await using var context = await LicenseServerTestContext.CreateAsync();
        var license = LicenseBuilder.Default().AddTo( context );
        var original = LeaseBuilder.For( license ).From( TestClock.Origin ).Lasting( 3 ).AddTo( context );

        Assert.Equal( 1, context.Repository.GetActiveSeats( license.LicenseId, TestClock.Days( 2 ) ) );

        context.Repository.CancelLease( original, "admin", TestClock.Days( 1 ) );
        await context.Repository.SaveChangesAsync();

        Assert.Equal( 0, context.Repository.GetActiveSeats( license.LicenseId, TestClock.Days( 2 ) ) );
    }

    /// <summary>
    /// A cancellation skips the adjustment of the end time that a new lease receives. With that
    /// adjustment, a lease that ends at the current instant would be rejected, because it does not
    /// end after the current instant, and the cancellation would have no effect.
    /// </summary>
    [Fact]
    public async Task CancelLease_IsNotRejectedForEndingImmediately()
    {
        await using var context = await LicenseServerTestContext.CreateAsync();
        var license = LicenseBuilder.Default().WithValidTo( TestClock.Days( 2 ) ).AddTo( context );
        var original = LeaseBuilder.For( license ).From( TestClock.Origin ).Lasting( 2 ).AddTo( context );

        context.Repository.CancelLease( original, "admin", TestClock.Days( 1 ) );
        await context.Repository.SaveChangesAsync();

        var replacement = await context.Db.Leases.SingleAsync( l => l.LeaseId != original.LeaseId );
        Assert.Equal( TestClock.Days( 1 ), replacement.EndTime );
    }

    [Fact]
    public async Task CancelLease_ThenRequestAgain_GrantsAFreshLease()
    {
        await using var context = await LicenseServerTestContext.CreateAsync();
        var license = LicenseBuilder.Default().AddTo( context );
        var original = LeaseBuilder.For( license ).User( "alice" ).Machine( "desktop-1" ).AddTo( context );

        context.Repository.CancelLease( original, "admin", TestClock.Days( 1 ) );
        await context.Repository.SaveChangesAsync();

        var lease = await context.LeaseService.GetLeaseAsync(
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