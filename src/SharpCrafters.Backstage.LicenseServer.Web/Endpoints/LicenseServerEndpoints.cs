using System.Diagnostics;
using System.Globalization;
using System.Xml;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using SharpCrafters.Backstage.LicenseServer.Data;
using SharpCrafters.Backstage.LicenseServer.Licensing;
using SharpCrafters.Backstage.LicenseServer.Locking;
using SharpCrafters.Backstage.LicenseServer.Options;
using SharpCrafters.Backstage.LicenseServer.Services;
using SharpCrafters.Common;

namespace SharpCrafters.Backstage.LicenseServer.Endpoints;

/// <summary>
/// The endpoints the PostSharp client talks to.
/// </summary>
/// <remarks>
/// The <c>.ashx</c> paths are part of the contract with the clients that are already deployed, so
/// the paths are kept although no ASP.NET handler serves them any more. The status codes and the
/// bodies of the responses are part of the same contract.
/// </remarks>
public static class LicenseServerEndpoints
{
    /// <summary>
    /// The synchronization point that a lease request reaches while it holds the lease lock.
    /// </summary>
    public const string HoldingLeaseLockSyncPoint = "LicenseServerEndpoints.GetLeaseAsync:HoldingLeaseLock";

    public static void MapLicenseServerEndpoints( this WebApplication app )
    {
        app.MapGet( "/Lease.ashx", GetLeaseAsync ).RequireAuthorization( AuthorizationPolicies.LeaseRequest );
        app.MapGet( "/GetTime.ashx", GetTime );
        app.MapGet( "/Admin/Export.ashx", ExportAsync ).RequireAuthorization( AuthorizationPolicies.Admin );
    }

    private static async Task<IResult> GetLeaseAsync(
        HttpContext context,
        LeaseService leaseService,
        ILeaseRepository repository,
        LeaseSerializer leaseSerializer,
        ILeaseLock leaseLock,
        IOptions<LicenseServerOptions> options,
        TimeProvider timeProvider,
        ILogger<LeaseService> logger,
        CancellationToken cancellationToken )
    {
        string? productCode = context.Request.Query["product"];

        // Clients older than PostSharp 5 do not send their version.
        string? versionString = context.Request.Query["version"];
        Version version;

        if ( string.IsNullOrEmpty( versionString ) )
        {
            version = new Version( 4, 9, 9 );
        }
        else if ( !Version.TryParse( versionString, out Version? parsedVersion ) )
        {
            return Error( 400, "Cannot parse the argument: version." );
        }
        else
        {
            version = parsedVersion;
        }

        string? buildDateString = context.Request.Query["buildDate"];
        DateTime? buildDate = null;

        if ( !string.IsNullOrEmpty( buildDateString ) )
        {
            if ( !DateTime.TryParse(
                    buildDateString,
                    CultureInfo.InvariantCulture,
                    DateTimeStyles.RoundtripKind,
                    out DateTime parsedBuildDate ) )
            {
                return Error( 400, "Cannot parse the argument: buildDate." );
            }

            buildDate = parsedBuildDate;
        }

        string? machine = context.Request.Query["machine"];

        if ( string.IsNullOrEmpty( machine ) )
        {
            return Error( 400, "Missing query string argument: machine." );
        }

        machine = machine.ToLowerInvariant();

        string? userName = context.Request.Query["user"];

        if ( string.IsNullOrEmpty( userName ) )
        {
            return Error( 400, "Missing query string argument: user." );
        }

        userName = userName.ToLowerInvariant();

        // An anonymous request has no name in ASP.NET Core, where WebForms returned an empty string.
        // The column does not accept null.
        string authenticatedUserName = context.User.Identity?.Name ?? string.Empty;

        await using IAsyncDisposable? handle = await leaseLock.TryAcquireAsync(
            options.Value.MutexTimeoutSpan,
            cancellationToken );

        if ( handle == null )
        {
            return Error( 503, "Service overloaded." );
        }

        // A test holds a request here, while it starts a second one, to prove that the second one
        // waits for the lock. Two requests that merely run at the same time do not prove it: they
        // pass whether the lock works or not. The service is absent in production, and the call then
        // costs a null check. See ITestSynchronizationProvider.
        ITestSynchronizationProvider? synchronization =
            context.RequestServices.GetService<ITestSynchronizationProvider>();

        if ( synchronization != null )
        {
            await synchronization.SyncPointAsync( HoldingLeaseLockSyncPoint, cancellationToken );
        }

        long startTimestamp = timeProvider.GetTimestamp();

        Dictionary<int, string> errors = [];

        GrantedLease? grantedLease = await leaseService.GetLicenseLeaseAsync(
            productCode,
            version,
            buildDate,
            machine,
            userName,
            authenticatedUserName,
            timeProvider.GetUtcNow().UtcDateTime,
            errors,
            cancellationToken );

        if ( grantedLease == null )
        {
            return Error( 403, "No license with free capacity. " + string.Join( " ", errors.Values ) );
        }

        await repository.SaveChangesAsync( cancellationToken );

        TimeSpan elapsed = timeProvider.GetElapsedTime( startTimestamp );

        if ( elapsed > TimeSpan.FromSeconds( 1 ) )
        {
            // The user name and the machine name come from the query string. The server removes the
            // characters with which they could forge a line in a plain-text log.
            logger.LogWarning(
                "The lease request for {User} on {Machine} took {Elapsed}.",
                Sanitize( userName ),
                Sanitize( machine ),
                elapsed );
        }

        return Results.Text(
            leaseSerializer.Serialize(
                grantedLease.LicenseKey,
                grantedLease.StartTime,
                grantedLease.EndTime,
                grantedLease.RenewTime ),
            "text/plain" );
    }

    /// <summary>
    /// Reports the current instant of the clock of the server, so that a simulator can synchronize
    /// its own accelerated clock.
    /// </summary>
    private static IResult GetTime( TimeProvider timeProvider, IOptions<LicenseServerOptions> options )
        => Results.Text(
            XmlConvert.ToString( timeProvider.GetUtcNow().UtcDateTime, XmlDateTimeSerializationMode.RoundtripKind )
            + ";"
            + XmlConvert.ToString( options.Value.TimeAcceleration ),
            "text/plain" );

    /// <summary>
    /// Streams the lease audit log for a range of months.
    /// </summary>
    private static async Task<IResult> ExportAsync(
        HttpContext context,
        ILeaseRepository repository,
        int? fy,
        int? fm,
        int? ty,
        int? tm,
        CancellationToken cancellationToken )
    {
        // The same range as the form offers. Without an upper bound, a year such as 10000 passes the
        // check and then raises an exception when the date is constructed.
        const int firstYear = 2010;
        const int lastYear = 2100;

        if ( fy is null or < firstYear or > lastYear
             || ty is null or < firstYear or > lastYear
             || fm is null or < 1 or > 12
             || tm is null or < 1 or > 12 )
        {
            return Results.BadRequest(
                $"The range of months is missing or invalid. Years must be between {firstYear} and {lastYear}." );
        }

        // The months are read as UTC, which is the time zone of every timestamp of the database.
        DateTime fromTime = new( fy.Value, fm.Value, 1, 0, 0, 0, DateTimeKind.Utc );
        DateTime toTime = new DateTime( ty.Value, tm.Value, 1, 0, 0, 0, DateTimeKind.Utc ).AddMonths( 1 );

        // The range of months is resolved to a range of lease identifiers, and every lease in that
        // range is exported. The legacy server selected the rows in the same way, and customers
        // compare the files they archived, so the selection is kept. It is the reason why a few
        // leases outside the requested months appear in the file.
        var bounds = await repository.Leases
            .Where( l => l.EndTime >= fromTime && l.StartTime <= toTime )
            .GroupBy( _ => 1 )
            .Select( g => new { MinLeaseId = g.Min( l => l.LeaseId ), MaxLeaseId = g.Max( l => l.LeaseId ) } )
            .SingleOrDefaultAsync( cancellationToken );

        string fileName =
            $"PostSharp_LicenseLog_{fy.Value}-{fm.Value}_{ty.Value}-{tm.Value}.txt";

        context.Response.Headers.ContentDisposition = $"attachment; filename={fileName}";

        if ( bounds == null )
        {
            return Results.Text( string.Empty, "text/plain" );
        }

        int minLeaseId = bounds.MinLeaseId;
        int maxLeaseId = bounds.MaxLeaseId;

        // The rows are written to the response as they arrive. An audit log that covers years of
        // activity is too large to assemble in memory, and the download starts before the server has
        // read the last row.
        return Results.Stream(
            async stream =>
            {
                await using StreamWriter writer = new( stream );

                IAsyncEnumerable<Lease> leases = repository.Leases
                    .Where( l => l.LeaseId >= minLeaseId && l.LeaseId <= maxLeaseId )
                    .OrderBy( l => l.LeaseId )
                    .AsNoTracking()
                    .AsAsyncEnumerable();

                await foreach ( Lease lease in leases.WithCancellation( cancellationToken ) )
                {
                    // The line is built in memory and written asynchronously. Writing the fields
                    // directly to the writer makes the writer flush synchronously when its buffer is
                    // full, and Kestrel refuses a synchronous write to a response body. One line is
                    // a hundred bytes. The constraint applies to the whole log, not to one line.
                    await writer.WriteLineAsync( lease.ToAuditLine() );
                }
            },
            "text/plain" );
    }

    private static IResult Error( int statusCode, string description )
        => Results.Text( description, "text/plain", statusCode: statusCode );

    /// <summary>
    /// Removes the control characters from a value of the request, so that the value cannot forge a
    /// line break in a log, and limits its length.
    /// </summary>
    private static string Sanitize( string value )
    {
        const int maximumLength = 200;

        string cleaned = new( value.Where( c => !char.IsControl( c ) ).ToArray() );

        return cleaned.Length <= maximumLength ? cleaned : cleaned[..maximumLength];
    }
}
