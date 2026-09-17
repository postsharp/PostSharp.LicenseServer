using System.Net;
using Microsoft.EntityFrameworkCore;
using PostSharp.LicenseServer.Data;
using PostSharp.LicenseServer.Tests.Fakes;
using PostSharp.LicenseServer.Tests.Infrastructure;

namespace PostSharp.LicenseServer.Tests;

/// <summary>
/// The contract with the PostSharp client: the URL, the query string, the status codes and the
/// body. Deployed clients depend on all four, so they are exercised through the real HTTP pipeline.
/// </summary>
public sealed class LeaseEndpointTests : IDisposable
{
    private readonly LicenseServerApplication application = new();

    public void Dispose() => this.application.Dispose();

    private static string Url(
        string? user = "alice",
        string? machine = "desktop-1",
        string? product = "Ultimate",
        string? version = "2025.1.0",
        string? buildDate = null )
    {
        List<string> arguments = [];

        if ( user != null ) { arguments.Add( $"user={user}" ); }
        if ( machine != null ) { arguments.Add( $"machine={machine}" ); }
        if ( product != null ) { arguments.Add( $"product={product}" ); }
        if ( version != null ) { arguments.Add( $"version={version}" ); }
        if ( buildDate != null ) { arguments.Add( $"buildDate={buildDate}" ); }

        return "/Lease.ashx?" + string.Join( "&", arguments );
    }

    [Fact]
    public async Task Lease_Succeeds_ReturnsTheSerializedLease()
    {
        this.application.AddLicense( LicenseBuilder.Default().WithUsers( 5 ) );
        HttpClient client = this.application.CreateClient();

        HttpResponseMessage response = await client.GetAsync( Url() );
        string body = await response.Content.ReadAsStringAsync();

        Assert.Equal( HttpStatusCode.OK, response.StatusCode );
        Assert.Equal( "text/plain", response.Content.Headers.ContentType?.MediaType );
        Assert.Contains( "License:", body, StringComparison.Ordinal );
        Assert.Contains( "StartTime:", body, StringComparison.Ordinal );
        Assert.Contains( "EndTime:", body, StringComparison.Ordinal );
        Assert.Contains( "RenewTime:", body, StringComparison.Ordinal );
    }

    [Fact]
    public async Task Lease_Succeeds_PersistsExactlyOneLease()
    {
        this.application.AddLicense( LicenseBuilder.Default().WithUsers( 5 ) );
        HttpClient client = this.application.CreateClient();

        await client.GetAsync( Url() );

        await using LicenseServerDbContext db = this.application.CreateDbContext();
        Lease lease = await db.Leases.SingleAsync();

        Assert.Equal( "alice", lease.UserName );
        Assert.Equal( "desktop-1", lease.Machine );
        Assert.Equal( "DOMAIN\\tester", lease.AuthenticatedUser );
    }

    [Fact]
    public async Task Lease_MixedCaseUserAndMachine_ArePersistedInLowerCase()
    {
        this.application.AddLicense( LicenseBuilder.Default().WithUsers( 5 ) );
        HttpClient client = this.application.CreateClient();

        await client.GetAsync( Url( user: "Alice", machine: "DESKTOP-1" ) );

        await using LicenseServerDbContext db = this.application.CreateDbContext();
        Lease lease = await db.Leases.SingleAsync();

        Assert.Equal( "alice", lease.UserName );
        Assert.Equal( "desktop-1", lease.Machine );
    }

    /// <summary>
    /// An anonymous request must still be served, and must still be recorded, even though the
    /// authenticated name is empty. The column does not accept null.
    /// </summary>
    [Fact]
    public async Task Lease_AnonymousRequest_IsServedAndRecorded()
    {
        this.application.AddLicense( LicenseBuilder.Default().WithUsers( 5 ) );
        HttpClient client = this.application.CreateClient();
        client.DefaultRequestHeaders.Add( TestAuthenticationHandler.AnonymousHeader, "1" );

        HttpResponseMessage response = await client.GetAsync( Url() );

        Assert.Equal( HttpStatusCode.OK, response.StatusCode );

        await using LicenseServerDbContext db = this.application.CreateDbContext();
        Lease lease = await db.Leases.SingleAsync();
        Assert.Equal( string.Empty, lease.AuthenticatedUser );
    }

    [Theory]
    [InlineData( null, "desktop-1", "Missing query string argument: user." )]
    [InlineData( "alice", null, "Missing query string argument: machine." )]
    public async Task Lease_MissingArgument_Returns400( string? user, string? machine, string expected )
    {
        HttpClient client = this.application.CreateClient();

        HttpResponseMessage response = await client.GetAsync( Url( user, machine ) );

        Assert.Equal( HttpStatusCode.BadRequest, response.StatusCode );
        Assert.Equal( expected, await response.Content.ReadAsStringAsync() );
    }

    [Fact]
    public async Task Lease_UnparseableVersion_Returns400()
    {
        HttpClient client = this.application.CreateClient();

        HttpResponseMessage response = await client.GetAsync( Url( version: "not-a-version" ) );

        Assert.Equal( HttpStatusCode.BadRequest, response.StatusCode );
        Assert.Equal( "Cannot parse the argument: version.", await response.Content.ReadAsStringAsync() );
    }

    [Fact]
    public async Task Lease_UnparseableBuildDate_Returns400()
    {
        HttpClient client = this.application.CreateClient();

        HttpResponseMessage response = await client.GetAsync( Url( buildDate: "yesterday" ) );

        Assert.Equal( HttpStatusCode.BadRequest, response.StatusCode );
        Assert.Equal( "Cannot parse the argument: buildDate.", await response.Content.ReadAsStringAsync() );
    }

    /// <summary>
    /// Clients older than PostSharp 5 send no version at all, and are treated as 4.9.9.
    /// </summary>
    [Fact]
    public async Task Lease_NoVersion_IsTreatedAsPostSharp499()
    {
        this.application.AddLicense(
            LicenseBuilder.Default().WithUsers( 5 ).WithMinPostSharpVersion( new Version( 5, 0, 0 ) ) );

        HttpClient client = this.application.CreateClient();

        HttpResponseMessage response = await client.GetAsync( Url( version: null ) );
        string body = await response.Content.ReadAsStringAsync();

        Assert.Equal( HttpStatusCode.Forbidden, response.StatusCode );
        Assert.Contains( "the requested version is 4.9.9", body, StringComparison.Ordinal );
    }

    [Fact]
    public async Task Lease_NoCapacity_Returns403WithTheReason()
    {
        this.application.AddLicense( LicenseBuilder.Default().NotLicenseServerEligible() );
        HttpClient client = this.application.CreateClient();

        HttpResponseMessage response = await client.GetAsync( Url() );
        string body = await response.Content.ReadAsStringAsync();

        Assert.Equal( HttpStatusCode.Forbidden, response.StatusCode );
        Assert.StartsWith( "No license with free capacity. ", body, StringComparison.Ordinal );
        Assert.Contains( "cannot be used in the license server", body, StringComparison.Ordinal );
    }

    [Fact]
    public async Task Lease_NoLicenseAtAll_Returns403()
    {
        HttpClient client = this.application.CreateClient();

        HttpResponseMessage response = await client.GetAsync( Url() );

        Assert.Equal( HttpStatusCode.Forbidden, response.StatusCode );
    }

    [Fact]
    public async Task Lease_LockTimesOut_Returns503()
    {
        this.application.AddLicense( LicenseBuilder.Default().WithUsers( 5 ) );
        this.application.LeaseLock = new NeverAcquiringLeaseLock();

        HttpClient client = this.application.CreateClient();

        HttpResponseMessage response = await client.GetAsync( Url() );

        Assert.Equal( HttpStatusCode.ServiceUnavailable, response.StatusCode );
        Assert.Equal( "Service overloaded.", await response.Content.ReadAsStringAsync() );
    }

    /// <summary>
    /// A build agent gets a licence key but never a stored lease, so that build machines cannot
    /// consume the seats of the developers they build for.
    /// </summary>
    [Fact]
    public async Task Lease_BuildAgent_IsServedWithoutConsumingASeat()
    {
        this.application.AddLicense( LicenseBuilder.Default().WithUsers( 5 ) );
        HttpClient client = this.application.CreateClient();

        HttpResponseMessage response = await client.GetAsync( Url( machine: "buildagent-1f2e" ) );

        Assert.Equal( HttpStatusCode.OK, response.StatusCode );

        await using LicenseServerDbContext db = this.application.CreateDbContext();
        Assert.Equal( 0, await db.Leases.CountAsync() );
    }

    [Fact]
    public async Task Lease_RequestedTwiceForTheSameMachine_ReusesTheLease()
    {
        this.application.AddLicense( LicenseBuilder.Default().WithUsers( 5 ) );
        HttpClient client = this.application.CreateClient();

        await client.GetAsync( Url() );
        await client.GetAsync( Url() );

        await using LicenseServerDbContext db = this.application.CreateDbContext();
        Assert.Equal( 1, await db.Leases.CountAsync() );
    }

    /// <summary>
    /// Concurrent requests must not each decide that the last free seat is theirs.
    /// </summary>
    [Fact]
    public async Task Lease_ConcurrentRequestsForTheSameMachine_GrantOneLease()
    {
        this.application.AddLicense( LicenseBuilder.Default().WithUsers( 5 ) );
        HttpClient client = this.application.CreateClient();

        HttpResponseMessage[] responses = await Task.WhenAll(
            Enumerable.Range( 0, 8 ).Select( _ => client.GetAsync( Url() ) ) );

        Assert.All( responses, r => Assert.Equal( HttpStatusCode.OK, r.StatusCode ) );

        await using LicenseServerDbContext db = this.application.CreateDbContext();
        Assert.Equal( 1, await db.Leases.CountAsync() );
    }
}
