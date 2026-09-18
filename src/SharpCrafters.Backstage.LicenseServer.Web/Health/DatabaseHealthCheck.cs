using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using SharpCrafters.Backstage.LicenseServer.Data;

namespace SharpCrafters.Backstage.LicenseServer.Health;

/// <summary>
/// Reports whether the database answers a query.
/// </summary>
/// <remarks>
/// The check reads the license table instead of opening a connection. The server never creates its
/// own schema on SQL Server, so a database that accepts connections but has no schema is a
/// deployment error. A query detects it; opening a connection does not.
/// </remarks>
public sealed class DatabaseHealthCheck( LicenseServerDbContext db, IHostEnvironment environment ) : IHealthCheck
{
    public async Task<HealthCheckResult> CheckHealthAsync(
        HealthCheckContext context,
        CancellationToken cancellationToken = default )
    {
        try
        {
            await db.Licenses.AsNoTracking().Select( l => l.LicenseId ).FirstOrDefaultAsync( cancellationToken );

            return HealthCheckResult.Healthy( "The database answers." );
        }
        catch ( Exception e ) when ( e is not OperationCanceledException )
        {
            return HealthCheckResult.Unhealthy(
                HealthDescriptions.ForException( "The database cannot be queried.", e, environment ),
                e );
        }
    }
}
