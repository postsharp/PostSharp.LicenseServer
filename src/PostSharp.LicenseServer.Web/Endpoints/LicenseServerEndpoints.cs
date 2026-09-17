using System.Diagnostics;
using System.Globalization;
using System.Xml;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using PostSharp.LicenseServer.Data;
using PostSharp.LicenseServer.Licensing;
using PostSharp.LicenseServer.Locking;
using PostSharp.LicenseServer.Options;
using PostSharp.LicenseServer.Services;

namespace PostSharp.LicenseServer.Endpoints;

/// <summary>
/// The endpoints the PostSharp client talks to.
/// </summary>
/// <remarks>
/// The <c>.ashx</c> paths are part of the contract with clients that are already deployed, so they
/// are kept literally even though nothing is handled by an ASP.NET handler any more. The status
/// codes and response bodies are equally part of that contract.
/// </remarks>
public static class LicenseServerEndpoints
{
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
        ILeaseSerializer leaseSerializer,
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

        // An anonymous request has no name at all in ASP.NET Core, where WebForms gave an empty
        // string. The column does not accept null.
        string authenticatedUserName = context.User.Identity?.Name ?? string.Empty;

        await using IAsyncDisposable? handle = await leaseLock.TryAcquireAsync(
            options.Value.MutexTimeoutSpan,
            cancellationToken );

        if ( handle == null )
        {
            return Error( 503, "Service overloaded." );
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
            logger.LogWarning( "The lease request for {User} on {Machine} took {Elapsed}.", userName, machine, elapsed );
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
    /// Reports the server's idea of the current time, so that a simulator can synchronize its own
    /// accelerated clock.
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
        if ( fy is null or < 1 || ty is null or < 1 || fm is null or < 1 or > 12 || tm is null or < 1 or > 12 )
        {
            return Results.BadRequest( "The range of months is missing or invalid." );
        }

        DateTime fromTime = new( fy.Value, fm.Value, 1 );
        DateTime toTime = new DateTime( ty.Value, tm.Value, 1 ).AddMonths( 1 );

        // The range of months is resolved to a range of lease identifiers, and everything in that
        // range is exported. The result is therefore a contiguous run of the log rather than a
        // filtered selection, which is what keeps the signature chain verifiable -- and it is why a
        // few leases outside the requested months can appear in the file.
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

        List<Lease> leases = await repository.Leases
            .Where( l => l.LeaseId >= bounds.MinLeaseId && l.LeaseId <= bounds.MaxLeaseId )
            .OrderBy( l => l.LeaseId )
            .AsNoTracking()
            .ToListAsync( cancellationToken );

        StringWriter writer = new();

        foreach ( Lease lease in leases )
        {
            lease.Write( writer, true );
            writer.WriteLine();
        }

        return Results.Text( writer.ToString(), "text/plain" );
    }

    private static IResult Error( int statusCode, string description )
        => Results.Text( description, "text/plain", statusCode: statusCode );
}
