using System.Net;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using SharpCrafters.Backstage.LicenseServer.Data;
using SharpCrafters.Backstage.LicenseServer.Tests.Infrastructure;

namespace SharpCrafters.Backstage.LicenseServer.Tests;

/// <summary>
/// The endpoints a monitoring system talks to. What matters to a probe is the status code, so each
/// test asserts it together with the body.
/// </summary>
public sealed class OperationsEndpointTests : IDisposable
{
    private readonly LicenseServerApplication application = new();

    public void Dispose() => this.application.Dispose();

    private async Task<(HttpStatusCode Status, JsonElement Body)> GetAsync( string url )
    {
        HttpResponseMessage response = await this.application.CreateClient().GetAsync( url );

        return (response.StatusCode, JsonDocument.Parse( await response.Content.ReadAsStringAsync() ).RootElement);
    }

    private static JsonElement Check( JsonElement body, string name )
        => body.GetProperty( "checks" ).EnumerateArray().Single( c => c.GetProperty( "name" ).GetString() == name );

    private static JsonElement DatabaseCheck( JsonElement body ) => Check( body, "database" );

    private static JsonElement LicenseCheck( JsonElement body ) => Check( body, "licenses" );

    [Fact]
    public async Task Health_LicenseWithFreeCapacity_IsHealthy()
    {
        this.application.AddLicense( LicenseBuilder.Default().WithUsers( 5 ) );

        (HttpStatusCode status, JsonElement body) = await this.GetAsync( "/health" );

        Assert.Equal( HttpStatusCode.OK, status );
        Assert.Equal( "Healthy", body.GetProperty( "status" ).GetString() );

        string[] checks = body.GetProperty( "checks" )
            .EnumerateArray()
            .Select( c => c.GetProperty( "name" ).GetString()! )
            .ToArray();

        Assert.Equal( ["database", "licenses"], checks );
    }

    /// <summary>
    /// A server with no license answers every lease request with 403 while looking perfectly well
    /// from the outside, which is the state the check exists to make visible. It warns rather than
    /// fails: the probe stays green, because nothing that watches a probe can add a license.
    /// </summary>
    [Fact]
    public async Task Health_NoLicense_WarnsAndStaysServed()
    {
        (HttpStatusCode status, JsonElement body) = await this.GetAsync( "/health" );

        Assert.Equal( HttpStatusCode.OK, status );
        Assert.Equal( "Degraded", body.GetProperty( "status" ).GetString() );

        JsonElement licenses = LicenseCheck( body );

        Assert.Equal( "Degraded", licenses.GetProperty( "status" ).GetString() );
        Assert.Equal( "No license is registered.", licenses.GetProperty( "description" ).GetString() );
    }

    [Fact]
    public async Task Health_ExpiredLicense_WarnsAndStaysServed()
    {
        this.application.AddLicense(
            LicenseBuilder.Default().WithUsers( 5 ).WithValidTo( TestClock.Days( -1 ) ) );

        (HttpStatusCode status, JsonElement body) = await this.GetAsync( "/health" );

        Assert.Equal( HttpStatusCode.OK, status );
        Assert.Equal( "Degraded", body.GetProperty( "status" ).GetString() );
        Assert.Contains( "1 expired", LicenseCheck( body ).GetProperty( "description" ).GetString()!, StringComparison.Ordinal );
    }

    /// <summary>
    /// A database without the schema is the deployment mistake the check exists for: the server never
    /// runs CreateTables.sql itself. The database is what fails the probe; the license check can only
    /// warn, and here it warns because it cannot read the state at all.
    /// </summary>
    /// <remarks>
    /// Neither check may answer with the message of the exception, because a database exception
    /// carries the name of the server and sometimes the whole connection string, and this endpoint is
    /// anonymous.
    /// </remarks>
    [Fact]
    public async Task Health_DatabaseWithoutSchema_FailsAndSaysNothingMore()
    {
        using ( LicenseServerDbContext db = this.application.CreateDbContext() )
        {
            await db.Database.ExecuteSqlRawAsync( "DROP TABLE Leases" );
            await db.Database.ExecuteSqlRawAsync( "DROP TABLE Licenses" );
        }

        (HttpStatusCode status, JsonElement body) = await this.GetAsync( "/health" );

        Assert.Equal( HttpStatusCode.ServiceUnavailable, status );
        Assert.Equal( "Unhealthy", DatabaseCheck( body ).GetProperty( "status" ).GetString() );
        Assert.Equal( "Degraded", LicenseCheck( body ).GetProperty( "status" ).GetString() );

        foreach ( JsonElement check in body.GetProperty( "checks" ).EnumerateArray() )
        {
            string description = check.GetProperty( "description" ).GetString()!;

            Assert.EndsWith( "The reason is in the log of the server.", description, StringComparison.Ordinal );
            Assert.DoesNotContain( "no such table", description, StringComparison.OrdinalIgnoreCase );
        }
    }

    /// <summary>
    /// The liveness probe answers on the state of the process alone. A license that has expired is
    /// not a reason to restart the server, and an orchestrator that restarted it would do so for ever.
    /// </summary>
    [Fact]
    public async Task Liveness_NoLicense_IsHealthy()
    {
        (HttpStatusCode status, JsonElement body) = await this.GetAsync( "/health/live" );

        Assert.Equal( HttpStatusCode.OK, status );
        Assert.Equal( "Healthy", body.GetProperty( "status" ).GetString() );
        Assert.Empty( body.GetProperty( "checks" ).EnumerateArray() );
    }

    [Fact]
    public async Task Version_ReportsTheProductAndTheLicensingLibrary()
    {
        (HttpStatusCode status, JsonElement body) = await this.GetAsync( "/version" );

        Assert.Equal( HttpStatusCode.OK, status );
        Assert.Equal( "SharpCrafters.Backstage.LicenseServer", body.GetProperty( "product" ).GetString() );

        // A version, not the empty string, and without the commit hash the build appends to it.
        string version = body.GetProperty( "version" ).GetString()!;

        Assert.NotEmpty( version );
        Assert.DoesNotContain( "+", version, StringComparison.Ordinal );

        Assert.True( Version.TryParse( body.GetProperty( "licensingLibrary" ).GetString(), out _ ) );
    }

    /// <summary>
    /// Both endpoints are served anonymously: a load balancer holds no Windows credentials, and a
    /// probe answered with 401 reads as a server that is down.
    /// </summary>
    [Theory]
    [InlineData( "/health/live" )]
    [InlineData( "/version" )]
    public async Task Endpoint_AnonymousRequest_IsServed( string url )
    {
        HttpClient client = this.application.CreateClient();
        client.DefaultRequestHeaders.Add( TestAuthenticationHandler.AnonymousHeader, "true" );

        HttpResponseMessage response = await client.GetAsync( url );

        Assert.Equal( HttpStatusCode.OK, response.StatusCode );
    }
}
