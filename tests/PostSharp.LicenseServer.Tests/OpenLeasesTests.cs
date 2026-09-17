using Microsoft.EntityFrameworkCore;
using PostSharp.LicenseServer.Tests.Infrastructure;

namespace PostSharp.LicenseServer.Tests;

/// <summary>
/// Leases are never updated in place: prolonging or cancelling one inserts a replacement that points
/// back at it. "Open" leases are those nothing points back at.
/// </summary>
public sealed class OpenLeasesTests
{
    [Fact]
    public async Task OpenLeases_LeaseNeverReplaced_IsOpen()
    {
        await using LicenseServerTestContext context = await LicenseServerTestContext.CreateAsync();
        License license = LicenseBuilder.Default().AddTo( context );
        Lease lease = LeaseBuilder.For( license ).AddTo( context );

        Assert.Equal( [lease.LeaseId], await context.Db.OpenLeases.Select( l => l.LeaseId ).ToListAsync() );
    }

    [Fact]
    public async Task OpenLeases_ReplacedLease_IsNotOpen()
    {
        await using LicenseServerTestContext context = await LicenseServerTestContext.CreateAsync();
        License license = LicenseBuilder.Default().AddTo( context );
        Lease original = LeaseBuilder.For( license ).AddTo( context );

        Lease? replacement = context.Repository.ProlongLease( original, "alice", TestClock.Days( 2.5 ) );
        await context.Repository.SaveChangesAsync();

        Assert.NotNull( replacement );
        Assert.Equal( [replacement.LeaseId], await context.Db.OpenLeases.Select( l => l.LeaseId ).ToListAsync() );
    }

    [Fact]
    public async Task OpenLeases_ChainOfReplacements_LeavesOnlyTheLast()
    {
        await using LicenseServerTestContext context = await LicenseServerTestContext.CreateAsync();
        License license = LicenseBuilder.Default().AddTo( context );

        Lease current = LeaseBuilder.For( license ).AddTo( context );

        for ( int i = 1; i <= 3; i++ )
        {
            current = context.Repository.ProlongLease( current, "alice", TestClock.Days( i * 2.5 ) )!;
            await context.Repository.SaveChangesAsync();
        }

        Assert.Equal( 4, await context.Db.Leases.CountAsync() );
        Assert.Equal( [current.LeaseId], await context.Db.OpenLeases.Select( l => l.LeaseId ).ToListAsync() );
    }

    /// <summary>
    /// Nothing in the schema prevents two leases from replacing the same lease. The legacy
    /// left-join query returned such a lease once per replacement; an anti-join returns it once.
    /// </summary>
    [Fact]
    public async Task OpenLeases_LeaseReplacedTwice_IsStillListedOnlyOnce()
    {
        await using LicenseServerTestContext context = await LicenseServerTestContext.CreateAsync();
        License license = LicenseBuilder.Default().AddTo( context );
        Lease original = LeaseBuilder.For( license ).AddTo( context );

        Lease survivor = LeaseBuilder.For( license ).User( "bob" ).AddTo( context );

        foreach ( int _ in Enumerable.Range( 0, 2 ) )
        {
            context.Db.Leases.Add(
                new Lease
                {
                    LicenseId = license.LicenseId,
                    OverwrittenLeaseId = original.LeaseId,
                    UserName = original.UserName,
                    Machine = original.Machine,
                    AuthenticatedUser = original.AuthenticatedUser,
                    StartTime = original.StartTime,
                    EndTime = TestClock.Days( 4 ),
                    HMAC = "x"
                } );
        }

        await context.Db.SaveChangesAsync();

        List<int> open = await context.Db.OpenLeases.Select( l => l.LeaseId ).ToListAsync();

        Assert.DoesNotContain( original.LeaseId, open );
        Assert.Equal( open.Count, open.Distinct().Count() );
        Assert.Contains( survivor.LeaseId, open );
    }

    [Fact]
    public async Task OpenLeases_TranslatesToSqlAsAnAntiJoin()
    {
        await using LicenseServerTestContext context = await LicenseServerTestContext.CreateAsync();

        string sql = context.Db.OpenLeases.ToQueryString();

        // Proves the filter runs in the database rather than after loading every lease.
        Assert.Contains( "NOT EXISTS", sql, StringComparison.OrdinalIgnoreCase );
    }
}
