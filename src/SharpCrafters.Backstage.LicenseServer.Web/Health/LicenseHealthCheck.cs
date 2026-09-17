using Microsoft.Extensions.Diagnostics.HealthChecks;
using SharpCrafters.Backstage.LicenseServer.Services;

namespace SharpCrafters.Backstage.LicenseServer.Health;

/// <summary>
/// Reports whether any license can serve a lease now.
/// </summary>
/// <remarks>
/// <para>
/// A server whose licenses have all expired, or are all full with their grace period over, answers
/// every request with 403 while looking perfectly well from the outside. That is the state this
/// check exists to make visible.
/// </para>
/// <para>
/// It warns and never fails: the result is <see cref="HealthStatus.Degraded"/>, which leaves the
/// endpoint answering 200 while the body and the log of the server name the problem. A license that
/// has expired is a state an administrator must act on, and it is not a state that restarting the
/// server, failing over to another one, or taking this one out of a load balancer improves. Only the
/// process and the database can make the probe fail.
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
            // check does, and that check is the one that fails the probe. It is caught here as well
            // because an exception left to the health check middleware becomes the description of the
            // failure, and the message of a database exception is not something an anonymous endpoint
            // should return.
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
