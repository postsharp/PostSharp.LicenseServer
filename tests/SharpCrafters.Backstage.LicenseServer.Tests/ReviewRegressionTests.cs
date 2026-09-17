using Microsoft.AspNetCore.Http;
using System.Net;
using Microsoft.AspNetCore.Mvc.Testing;
using PostSharp.LicenseServer.Endpoints;
using PostSharp.LicenseServer.Tests.Infrastructure;

namespace PostSharp.LicenseServer.Tests;

/// <summary>
/// Defects found while reviewing the migration. Each of these passed unnoticed because the legacy
/// implementation behaved the same way.
/// </summary>
public sealed class ReviewRegressionTests : IDisposable
{
    private readonly LicenseServerApplication application = new();

    public void Dispose() => this.application.Dispose();

    /// <summary>
    /// A build agent is exempt from consuming a seat, not from the rules about which licenses may be
    /// served. The legacy code only checked that the key parsed.
    /// </summary>
    [Fact]
    public async Task BuildAgent_IneligibleLicense_IsNotServed()
    {
        this.application.AddLicense( LicenseBuilder.Default().NotLicenseServerEligible() );
        HttpClient client = this.application.CreateClient();

        HttpResponseMessage response = await client.GetAsync(
            "/Lease.ashx?user=alice&machine=buildagent-1f2e&product=Ultimate&version=2025.1.0" );

        Assert.Equal( HttpStatusCode.Forbidden, response.StatusCode );
        Assert.Contains(
            "cannot be used in the license server",
            await response.Content.ReadAsStringAsync(),
            StringComparison.Ordinal );
    }

    [Fact]
    public async Task BuildAgent_LicenseRequiringANewerClient_IsNotServed()
    {
        this.application.AddLicense(
            LicenseBuilder.Default().WithMinPostSharpVersion( new Version( 2099, 1, 0 ) ) );

        HttpClient client = this.application.CreateClient();

        HttpResponseMessage response = await client.GetAsync(
            "/Lease.ashx?user=alice&machine=buildagent-1f2e&product=Ultimate&version=2025.1.0" );

        Assert.Equal( HttpStatusCode.Forbidden, response.StatusCode );
    }

    /// <summary>
    /// An expired license must not be handed to a build agent with three more days on it.
    /// </summary>
    [Fact]
    public async Task BuildAgent_ExpiredLicense_IsNotServed()
    {
        this.application.AddLicense(
            LicenseBuilder.Default().WithValidTo( new DateTime( 2020, 1, 1, 0, 0, 0, DateTimeKind.Utc ) ) );

        HttpClient client = this.application.CreateClient();

        HttpResponseMessage response = await client.GetAsync(
            "/Lease.ashx?user=alice&machine=buildagent-1f2e&product=Ultimate&version=2025.1.0" );

        Assert.Equal( HttpStatusCode.Forbidden, response.StatusCode );
    }

    [Fact]
    public async Task BuildAgent_ValidLicense_IsStillServedWithoutALease()
    {
        this.application.AddLicense( LicenseBuilder.Default().WithUsers( 5 ) );
        HttpClient client = this.application.CreateClient();

        HttpResponseMessage response = await client.GetAsync(
            "/Lease.ashx?user=alice&machine=buildagent-1f2e&product=Ultimate&version=2025.1.0" );

        Assert.Equal( HttpStatusCode.OK, response.StatusCode );

        await using var db = this.application.CreateDbContext();
        Assert.Empty( db.Leases );
    }

    /// <summary>
    /// A license with no seat limit has no capacity to exceed, so there is no grace period. Reaching
    /// the grace pass with one used to dereference a null maximum and answer 500 instead of denying
    /// the request.
    /// </summary>
    [Fact]
    public async Task UnlimitedButExpiredLicense_IsDeniedRatherThanFailing()
    {
        this.application.AddLicense(
            LicenseBuilder.Default()
                .WithUsers( null )
                .WithValidTo( new DateTime( 2020, 1, 1, 0, 0, 0, DateTimeKind.Utc ) ) );

        HttpClient client = this.application.CreateClient();

        HttpResponseMessage response = await client.GetAsync(
            "/Lease.ashx?user=alice&machine=desktop-1&product=Ultimate&version=2025.1.0" );

        Assert.Equal( HttpStatusCode.Forbidden, response.StatusCode );
    }

    /// <summary>
    /// A year outside the range of a date used to pass validation and then throw while the date was
    /// being constructed.
    /// </summary>
    [Theory]
    [InlineData( "fy=10000&fm=1&ty=10000&tm=2" )]
    [InlineData( "fy=0&fm=1&ty=2026&tm=2" )]
    [InlineData( "fy=2026&fm=13&ty=2026&tm=1" )]
    [InlineData( "fy=2026&fm=1&ty=2026&tm=0" )]
    public async Task Export_OutOfRangeMonthOrYear_Returns400( string query )
    {
        HttpClient client = this.application.CreateClient();

        Assert.Equal(
            HttpStatusCode.BadRequest,
            ( await client.GetAsync( $"/Admin/Export.ashx?{query}" ) ).StatusCode );
    }

    [Fact]
    public async Task Export_ValidRange_IsStillServed()
    {
        HttpClient client = this.application.CreateClient();

        Assert.Equal(
            HttpStatusCode.OK,
            ( await client.GetAsync( "/Admin/Export.ashx?fy=2026&fm=1&ty=2026&tm=12" ) ).StatusCode );
    }

    /// <summary>
    /// Installed below a site root, a redirect has to keep the path base or it lands on the parent
    /// site.
    /// </summary>
    [Theory]
    [InlineData( "/LicenseServer", "/Graph", "?id=5", "/LicenseServer/Graph?id=5" )]
    [InlineData( "/LicenseServer", "/", "", "/LicenseServer" )]
    [InlineData( "/LicenseServer", "/Admin/Details", "?id=5", "/LicenseServer/Admin/Details?id=5" )]
    [InlineData( "", "/Graph", "?id=5", "/Graph?id=5" )]
    [InlineData( "", "/", "", "/" )]
    public void LegacyUrl_KeepsThePathBaseAndTheQueryString(
        string pathBase,
        string target,
        string queryString,
        string expected )
    {
        Assert.Equal(
            expected,
            LegacyUrlRedirects.BuildRedirectLocation(
                new PathString( pathBase.Length == 0 ? null : pathBase ),
                target,
                new QueryString( queryString.Length == 0 ? null : queryString ) ) );
    }
}
