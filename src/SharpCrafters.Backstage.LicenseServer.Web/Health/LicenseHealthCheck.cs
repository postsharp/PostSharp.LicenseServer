using Microsoft.Extensions.Diagnostics.HealthChecks;
using SharpCrafters.Backstage.LicenseServer.Services;

namespace SharpCrafters.Backstage.LicenseServer.Health;

/// <summary>
/// Reports whether any license can serve a lease now.
/// </summary>
/// <remarks>
/// <para>
/// A server whose licenses are all expired, or all full with their grace period ended, answers every
/// lease request with 403 while its process and its database work. This check reports that state.
/// </para>
/// <para>
/// It warns and never fails. The result is <see cref="HealthStatus.Degraded"/>, so the endpoint
/// answers 200, and the body of the response and the log of the server name the problem. An expired
/// license requires an action from an administrator. Restarting the server, failing over to another
/// server, and removing this server from a load balancer do not improve that state. Only the process
/// and the database can make the probe fail.
/// </para>
/// </remarks>
public sealed class LicenseHealthCheck(
    LicenseAvailabilityService availability,
    TimeProvider timeProvider,
    IHostEnvironment environment ) : IHealthCheck
{
    public async Task<HealthCheckResult> CheckHealthAsync(
        HealthCheckContext context,
        CancellationToken cancellationToken = default )
    {
        LicenseAvailability result;

        try
        {
            result = await availability.GetAvailabilityAsync( timeProvider.GetUtcNow().UtcDateTime, cancellationToken );
        }
        catch ( Exception e ) when ( e is not OperationCanceledException )
        {
            // Reading the license state reads the database, so this check fails whenever the database
            // check fails, and the database check is the one that fails the probe. The exception is
            // caught here as well, because the health check middleware uses the message of an
            // uncaught exception as the description of the failure, and an anonymous endpoint must
            // not return the message of a database exception.
            return HealthCheckResult.Degraded(
                HealthDescriptions.ForException( "The license state cannot be read.", e, environment ),
                e );
        }

        IReadOnlyDictionary<string, object> data = new Dictionary<string, object>
        {
            ["available"] = result.Available,
            ["exhausted"] = result.Exhausted,
            ["expired"] = result.Expired,
            ["invalid"] = result.Invalid,
            ["disabled"] = result.Disabled,
            ["total"] = result.Total
        };

        return result.CanServeLease
            ? HealthCheckResult.Healthy( result.Describe(), data )
            : HealthCheckResult.Degraded( result.Describe(), data: data );
    }
}
