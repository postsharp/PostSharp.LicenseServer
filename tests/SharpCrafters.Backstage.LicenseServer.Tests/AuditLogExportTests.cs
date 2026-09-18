using System.Net;
using Microsoft.AspNetCore.Mvc.Testing;
using SharpCrafters.Backstage.LicenseServer.Data;
using SharpCrafters.Backstage.LicenseServer.Tests.Infrastructure;

namespace SharpCrafters.Backstage.LicenseServer.Tests;

/// <summary>
/// The export of the audit log. It writes the rows to the response as it reads them, and it does not
/// build the whole file in memory.
/// </summary>
public sealed class AuditLogExportTests : IDisposable
{
    /// <summary>
    /// A number of leases large enough that the writer of the response flushes before it writes the
    /// last one. A <see cref="StreamWriter"/> buffers about a thousand characters, and an audit line
    /// has about ninety, so a few leases stay in the buffer and exercise nothing. The export returned
    /// a truncated response in production while it passed a test that wrote three lines.
    /// </summary>
    private const int leaseCount = 60;

    private readonly LicenseServerApplication application = new();

    public void Dispose() => this.application.Dispose();

    [Fact]
    public async Task Export_WithMoreLeasesThanTheWriterBuffers_StreamsThemAll()
    {
        License license = this.application.AddLicense( LicenseBuilder.Default().WithUsers( 5 ) );
        this.SeedLeases( license, leaseCount );

        HttpClient client = this.application.CreateClient();

        // The response body refuses a synchronous write, exactly as Kestrel does. TestServer accepts
        // one whatever its AllowSynchronousIO property says, so without this guard an endpoint that
        // writes synchronously passes here and fails against a real server.
        this.application.ResponseBody.IsEnabled = true;

        HttpResponseMessage response = await client.GetAsync( this.ExportUrl( 1, 12 ) );

        Assert.Equal( HttpStatusCode.OK, response.StatusCode );

        string[] lines = (await response.Content.ReadAsStringAsync())
            .Split( '\n', StringSplitOptions.RemoveEmptyEntries );

        Assert.Equal( leaseCount, lines.Length );

        foreach ( string line in lines )
        {
            // The identifier, the overwritten lease, the license, the two instants and the two
            // hashed names.
            string[] fields = line.TrimEnd( '\r' ).Split( ';' );

            Assert.Equal( 7, fields.Length );
        }

        Assert.DoesNotContain( "alice", string.Join( "", lines ), StringComparison.OrdinalIgnoreCase );
    }

    /// <summary>
    /// The download is offered as a file, named after the range that was asked for.
    /// </summary>
    [Fact]
    public async Task Export_WithLeases_IsOfferedAsAFile()
    {
        License license = this.application.AddLicense( LicenseBuilder.Default().WithUsers( 5 ) );
        this.SeedLeases( license, 1 );

        HttpClient client = this.application.CreateClient();
        this.application.ResponseBody.IsEnabled = true;

        HttpResponseMessage response = await client.GetAsync( this.ExportUrl( 3, 4 ) );

        Assert.Equal( HttpStatusCode.OK, response.StatusCode );

        Assert.Equal(
            $"attachment; filename=PostSharp_LicenseLog_{DateTime.UtcNow.Year}-3_{DateTime.UtcNow.Year}-4.txt",
            response.Content.Headers.ContentDisposition?.ToString() );
    }

    /// <summary>
    /// A range that contains no lease is answered with an empty body. The earlier tests of the export
    /// all followed this path, because the database they exported contained no lease.
    /// </summary>
    [Fact]
    public async Task Export_WithNoLease_IsEmpty()
    {
        HttpClient client = this.application.CreateClient();
        this.application.ResponseBody.IsEnabled = true;

        HttpResponseMessage response = await client.GetAsync( this.ExportUrl( 1, 12 ) );

        Assert.Equal( HttpStatusCode.OK, response.StatusCode );
        Assert.Equal( "", await response.Content.ReadAsStringAsync() );
    }

    /// <summary>
    /// The leases are written directly to the database and not requested, so that their number does
    /// not depend on the capacity of the license or on the rules of the allocator.
    /// </summary>
    private void SeedLeases( License license, int count )
    {
        using LicenseServerDbContext db = this.application.CreateDbContext();

        DateTime start = DateTime.UtcNow.Date.AddDays( -1 );

        for ( int i = 0; i < count; i++ )
        {
            db.Leases.Add(
                new Lease
                {
                    LicenseId = license.LicenseId,
                    StartTime = start,
                    EndTime = start.AddDays( 3 ),
                    UserName = $"alice.{i:D3}",
                    Machine = $"desktop-{i:D3}",
                    AuthenticatedUser = "DOMAIN\\tester"
                } );
        }

        db.SaveChanges();
    }

    private string ExportUrl( int fromMonth, int toMonth )
    {
        int year = DateTime.UtcNow.Year;

        return $"/Admin/Export.ashx?fy={year}&fm={fromMonth}&ty={year}&tm={toMonth}";
    }
}
