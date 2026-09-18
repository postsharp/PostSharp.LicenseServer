// Copyright (c) SharpCrafters s.r.o. See the LICENSE.md file in the root directory of this repository root for details.

using System.Net;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using SharpCrafters.Backstage.LicenseServer.Data;
using SharpCrafters.Backstage.LicenseServer.Tests.Infrastructure;

namespace SharpCrafters.Backstage.LicenseServer.Tests;

/// <summary>
/// The endpoints of a monitoring system. A probe reads the status code, so each test asserts the
/// status code together with the body.
/// </summary>
public sealed class OperationsEndpointTests : IDisposable
{
    private readonly LicenseServerApplication application = new();

    public void Dispose() => this.application.Dispose();

    private async Task<(HttpStatusCode Status, JsonElement Body)> GetAsync( string url )
    {
        var response = await this.application.CreateClient().GetAsync( url );

        return ( response.StatusCode, JsonDocument.Parse( await response.Content.ReadAsStringAsync() ).RootElement );
    }

    private static JsonElement Check( JsonElement body, string name )
        => body.GetProperty( "checks" ).EnumerateArray().Single( c => c.GetProperty( "name" ).GetString() == name );

    private static JsonElement DatabaseCheck( JsonElement body ) => Check( body, "database" );

    private static JsonElement LicenseCheck( JsonElement body ) => Check( body, "licenses" );

    [Fact]
    public async Task Health_LicenseWithFreeCapacity_IsHealthy()
    {
        this.application.AddLicense( LicenseBuilder.Default().WithUsers( 5 ) );

        var (status, body) = await this.GetAsync( "/health" );

        Assert.Equal( HttpStatusCode.OK, status );
        Assert.Equal( "Healthy", body.GetProperty( "status" ).GetString() );

        var checks = body.GetProperty( "checks" )
            .EnumerateArray()
            .Select( c => c.GetProperty( "name" ).GetString()! )
            .ToArray();

        Assert.Equal( ["database", "licenses"], checks );
    }

    /// <summary>
    /// A server without a license answers every lease request with 403 while its process and its
    /// database work. The check reports that state. It warns and does not fail, so the probe still
    /// succeeds, because no system that reads a probe can add a license.
    /// </summary>
    [Fact]
    public async Task Health_NoLicense_WarnsAndStaysServed()
    {
        var (status, body) = await this.GetAsync( "/health" );

        Assert.Equal( HttpStatusCode.OK, status );
        Assert.Equal( "Degraded", body.GetProperty( "status" ).GetString() );

        var licenses = LicenseCheck( body );

        Assert.Equal( "Degraded", licenses.GetProperty( "status" ).GetString() );
        Assert.Equal( "No license is registered.", licenses.GetProperty( "description" ).GetString() );
    }

    [Fact]
    public async Task Health_ExpiredLicense_WarnsAndStaysServed()
    {
        this.application.AddLicense( LicenseBuilder.Default().WithUsers( 5 ).WithValidTo( TestClock.Days( -1 ) ) );

        var (status, body) = await this.GetAsync( "/health" );

        Assert.Equal( HttpStatusCode.OK, status );
        Assert.Equal( "Degraded", body.GetProperty( "status" ).GetString() );
        Assert.Contains( "1 expired", LicenseCheck( body ).GetProperty( "description" ).GetString()!, StringComparison.Ordinal );
    }

    /// <summary>
    /// A database without the schema is the deployment error that this check detects, because the
    /// server never runs CreateTables.sql itself. The database check fails the probe. The license
    /// check only warns, and here it warns because it cannot read the state.
    /// </summary>
    /// <remarks>
    /// Neither check returns the message of the exception. A database exception contains the name of
    /// the server, and sometimes the whole connection string, and this endpoint requires no
    /// authentication.
    /// </remarks>
    [Fact]
    public async Task Health_DatabaseWithoutSchema_FailsAndSaysNothingMore()
    {
        // The database keeps no schema after this test, so it must not serve another one.
        this.application.DoNotReuseDatabase();

        using ( var db = this.application.CreateDbContext() )
        {
            // PostgreSQL folds an identifier that is not quoted to lower case, and the tables of this
            // schema keep their capitals. The quotation marks are accepted by all three engines.
            await db.Database.ExecuteSqlRawAsync( "DROP TABLE \"Leases\"" );
            await db.Database.ExecuteSqlRawAsync( "DROP TABLE \"Licenses\"" );
        }

        var (status, body) = await this.GetAsync( "/health" );

        Assert.Equal( HttpStatusCode.ServiceUnavailable, status );
        Assert.Equal( "Unhealthy", DatabaseCheck( body ).GetProperty( "status" ).GetString() );
        Assert.Equal( "Degraded", LicenseCheck( body ).GetProperty( "status" ).GetString() );

        foreach ( var check in body.GetProperty( "checks" ).EnumerateArray() )
        {
            var description = check.GetProperty( "description" ).GetString()!;

            Assert.EndsWith( "The reason is in the log of the server.", description, StringComparison.Ordinal );
            Assert.DoesNotContain( "no such table", description, StringComparison.OrdinalIgnoreCase );
        }
    }

    /// <summary>
    /// The liveness probe reports the state of the process only. An expired license is not a reason
    /// to restart the server, and an orchestrator that restarted it would restart it indefinitely.
    /// </summary>
    [Fact]
    public async Task Liveness_NoLicense_IsHealthy()
    {
        var (status, body) = await this.GetAsync( "/health/live" );

        Assert.Equal( HttpStatusCode.OK, status );
        Assert.Equal( "Healthy", body.GetProperty( "status" ).GetString() );
        Assert.Empty( body.GetProperty( "checks" ).EnumerateArray() );
    }

    [Fact]
    public async Task Version_ReportsTheProductAndTheLicensingLibrary()
    {
        var (status, body) = await this.GetAsync( "/version" );

        Assert.Equal( HttpStatusCode.OK, status );
        Assert.Equal( "SharpCrafters.Backstage.LicenseServer", body.GetProperty( "product" ).GetString() );

        // A version, not the empty string, and without the commit hash the build appends to it.
        var version = body.GetProperty( "version" ).GetString()!;

        Assert.NotEmpty( version );
        Assert.DoesNotContain( "+", version, StringComparison.Ordinal );

        Assert.True( Version.TryParse( body.GetProperty( "licensingLibrary" ).GetString(), out _ ) );
    }

    /// <summary>
    /// Both endpoints require no authentication. A monitoring agent has no Windows credentials, and
    /// a probe that receives 401 reports the server as unavailable.
    /// </summary>
    [Theory]
    [InlineData( "/health/live" )]
    [InlineData( "/version" )]
    public async Task Endpoint_AnonymousRequest_IsServed( string url )
    {
        var client = this.application.CreateClient();
        client.DefaultRequestHeaders.Add( TestAuthenticationHandler.AnonymousHeader, "true" );

        var response = await client.GetAsync( url );

        Assert.Equal( HttpStatusCode.OK, response.StatusCode );
    }
}