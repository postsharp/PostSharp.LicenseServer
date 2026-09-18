using System.Reflection;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using SharpCrafters.Backstage.LicenseServer.Licensing;

namespace SharpCrafters.Backstage.LicenseServer.Endpoints;

/// <summary>
/// The endpoints of a monitoring system. They report the state of the server and the version that is
/// deployed.
/// </summary>
/// <remarks>
/// These endpoints require no authentication, as <c>Lease.ashx</c> does not, and unlike the
/// administrative pages. A load balancer and a monitoring agent have no Windows credentials, so an
/// endpoint behind authentication would answer 401, and the probe would report the server as
/// unavailable. The responses contain no license key, no user name and no connection string. The
/// version number is readable by anyone who can reach the server.
/// </remarks>
public static class OperationsEndpoints
{
    /// <summary>
    /// The path of the monitoring probe. It reports the state of the server and of the resources the
    /// server needs.
    /// </summary>
    public const string HealthPath = "/health";

    /// <summary>
    /// The path of the liveness probe. It reports whether the process answers.
    /// </summary>
    public const string LivenessPath = "/health/live";

    public const string VersionPath = "/version";

    public static void MapOperationsEndpoints( this WebApplication app )
    {
        app.MapHealthChecks( HealthPath, new HealthCheckOptions { ResponseWriter = WriteHealthAsync } );

        // No check runs here. The response reports the state of the process, which is the state a
        // container orchestrator restarts on. Restarting the server repairs neither a database that
        // is down nor a license that has expired.
        app.MapHealthChecks(
            LivenessPath,
            new HealthCheckOptions { Predicate = _ => false, ResponseWriter = WriteHealthAsync } );

        app.MapGet( VersionPath, GetVersion );
    }

    /// <summary>
    /// Writes the report as JSON. The default writer answers with the single word <c>Healthy</c>,
    /// which does not say which check failed.
    /// </summary>
    private static Task WriteHealthAsync( HttpContext context, HealthReport report )
        => context.Response.WriteAsJsonAsync(
            new
            {
                status = report.Status.ToString(),
                checks = report.Entries
                    .Select(
                        entry => new
                        {
                            name = entry.Key,
                            status = entry.Value.Status.ToString(),
                            description = entry.Value.Description
                        } )
                    .ToArray()
            } );

    /// <summary>
    /// Reports the build that is deployed, and the version of the licensing library that parses the
    /// license keys. The version of the library says whether the server accepts a license key that
    /// requires a recent library.
    /// </summary>
    private static IResult GetVersion( ILicenseServerVersion version )
        => Results.Json(
            new
            {
                product = ProductName,
                version = ProductVersion,
                licensingLibrary = version.LicensingLibraryVersion.ToString()
            } );

    private static string ProductName { get; } =
        typeof(OperationsEndpoints).Assembly.GetName().Name ?? "SharpCrafters.Backstage.LicenseServer";

    /// <summary>
    /// The informational version of this assembly, without the commit hash that the build appends to
    /// it after a <c>+</c>.
    /// </summary>
    private static string ProductVersion { get; } = ReadProductVersion();

    private static string ReadProductVersion()
    {
        Assembly assembly = typeof(OperationsEndpoints).Assembly;

        string? informationalVersion = assembly
            .GetCustomAttribute<AssemblyInformationalVersionAttribute>()?
            .InformationalVersion;

        if ( string.IsNullOrEmpty( informationalVersion ) )
        {
            return assembly.GetName().Version?.ToString() ?? "unknown";
        }

        int metadata = informationalVersion.IndexOf( '+', StringComparison.Ordinal );

        return metadata < 0 ? informationalVersion : informationalVersion[..metadata];
    }
}
