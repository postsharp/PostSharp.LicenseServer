using System.Net;
using Microsoft.EntityFrameworkCore;
using SharpCrafters.Backstage.LicenseServer.Data;
using SharpCrafters.Backstage.LicenseServer.Endpoints;
using SharpCrafters.Backstage.LicenseServer.Tests.Infrastructure;

namespace SharpCrafters.Backstage.LicenseServer.Tests;

/// <summary>
/// The lock that serializes the lease requests, so that two requests cannot both take the last free
/// seat.
/// </summary>
/// <remarks>
/// The lock belongs to the database, so these tests exercise the lock of the engine the run uses: an
/// application lock on SQL Server, and a write transaction on SQLite. They drive the interleaving
/// with a synchronization point instead of starting two requests and hoping that they overlap. Two
/// requests that merely run at the same time prove nothing: they pass whether the lock works or not.
/// </remarks>
public sealed class LeaseLockTests : IDisposable
{
    private readonly LicenseServerApplication application = new();

    public void Dispose() => this.application.Dispose();

    private static string Url( string user, string machine )
        => $"/Lease.ashx?user={user}&machine={machine}&product=Ultimate&version=2027.0.0";

    /// <summary>
    /// The second request waits for the first one, sees the seat it took, and is denied. Without the
    /// lock it would read the seat count before the first request wrote its lease, and both would be
    /// granted.
    /// </summary>
    [Fact]
    public async Task SecondRequest_WaitsForTheFirst_AndSeesItsLease()
    {
        License license = this.application.AddLicense( LicenseBuilder.Default().WithUsers( 1 ).WithGracePercent( 0 ) );

        this.application.Synchronization.EnableSyncPoint( LicenseServerEndpoints.HoldingLeaseLockSyncPoint );

        HttpClient client = this.application.CreateClient();

        Task<HttpResponseMessage> first = client.GetAsync( Url( "alice", "desktop-1" ) );

        // The first request now holds the lock.
        await this.application.Synchronization.WaitForSyncPointReachedAsync(
            LicenseServerEndpoints.HoldingLeaseLockSyncPoint );

        Task<HttpResponseMessage> second = client.GetAsync( Url( "bob", "desktop-2" ) );

        // Releases the first request, and lets the second one through the synchronization point when
        // it finally acquires the lock.
        this.application.Synchronization.DisableSyncPoint( LicenseServerEndpoints.HoldingLeaseLockSyncPoint );

        HttpResponseMessage firstResponse = await first;
        HttpResponseMessage secondResponse = await second;

        Assert.Equal( HttpStatusCode.OK, firstResponse.StatusCode );
        Assert.Equal( HttpStatusCode.Forbidden, secondResponse.StatusCode );

        await using LicenseServerDbContext db = this.application.CreateDbContext();

        Assert.Equal( 1, await db.Leases.CountAsync( l => l.LicenseId == license.LicenseId ) );
    }

    /// <summary>
    /// A request that waits longer than <c>MutexTimeout</c> for the lock is answered with the status
    /// 503, which is what the legacy server answered when its mutex timed out.
    /// </summary>
    [Fact]
    public async Task SecondRequest_WhenTheFirstHoldsTheLockTooLong_IsAnsweredWithServiceOverloaded()
    {
        this.application.MutexTimeoutSeconds = 1;
        this.application.AddLicense( LicenseBuilder.Default().WithUsers( 5 ) );

        this.application.Synchronization.EnableSyncPoint( LicenseServerEndpoints.HoldingLeaseLockSyncPoint );

        HttpClient client = this.application.CreateClient();

        Task<HttpResponseMessage> first = client.GetAsync( Url( "alice", "desktop-1" ) );

        await this.application.Synchronization.WaitForSyncPointReachedAsync(
            LicenseServerEndpoints.HoldingLeaseLockSyncPoint );

        // The first request holds the lock for longer than the second one waits.
        HttpResponseMessage secondResponse = await client.GetAsync( Url( "bob", "desktop-2" ) );

        Assert.Equal( HttpStatusCode.ServiceUnavailable, secondResponse.StatusCode );
        Assert.Equal( "Service overloaded.", await secondResponse.Content.ReadAsStringAsync() );

        this.application.Synchronization.DisableSyncPoint( LicenseServerEndpoints.HoldingLeaseLockSyncPoint );

        Assert.Equal( HttpStatusCode.OK, (await first).StatusCode );
    }
}
