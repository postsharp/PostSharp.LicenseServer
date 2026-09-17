using System.Reflection;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using SharpCrafters.Backstage.LicenseServer.Licensing;

namespace SharpCrafters.Backstage.LicenseServer.Endpoints;

/// <summary>
/// The endpoints a monitoring system talks to: what state the server is in, and which version of it
/// is deployed.
/// </summary>
/// <remarks>
/// They are served anonymously, like <c>Lease.ashx</c> and unlike the administrative pages. A load
/// balancer or a monitoring agent holds no Windows credentials, so a probe behind authentication
/// would answer 401 and be read as a server that is down. Nothing here discloses a license key, a
/// user name or a connection string; the version number is disclosed to whoever can reach the
/// server, which is the price of a probe that works.
/// </remarks>
public static class OperationsEndpoints
{
    /// <summary>
    /// The state of the server and of everything it needs: a monitoring probe.
    /// </summary>
    public const string HealthPath = "/health";

    /// <summary>
    /// Whether the process answers at all: a liveness probe.
    /// </summary>
    public const string LivenessPath = "/health/live";

    public const string VersionPath = "/version";

    public static void MapOperationsEndpoints( this WebApplication app )
    {
        app.MapHealthChecks( HealthPath, new HealthCheckOptions { ResponseWriter = WriteHealthAsync } );

        // No check runs here. The answer is the process itself, which is what a container orchestrator
        // should restart on: a database that is down and a license that has expired are both states
        // that restarting the server does not mend.
        app.MapHealthChecks(
            LivenessPath,
            new HealthCheckOptions { Predicate = _ => false, ResponseWriter = WriteHealthAsync } );

        app.MapGet( VersionPath, GetVersion );
    }

    /// <summary>
    /// Writes the report as JSON. The default writer answers with the single word <c>Healthy</c>,
    /// which says nothing about which of the checks failed.
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
    /// Reports which build is deployed, and which version of the licensing library it parses license
    /// keys with. The second answers whether a license key that requires a recent library is served.
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
