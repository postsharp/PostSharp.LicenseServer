using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using SharpCrafters.Backstage.LicenseServer.Data;

namespace SharpCrafters.Backstage.LicenseServer.Health;

/// <summary>
/// Reports whether the database answers a query.
/// </summary>
/// <remarks>
/// The check reads the license table rather than opening a connection, because the server never
/// creates its own schema on SQL Server: a database that accepts connections but was never given
/// <c>CreateTables.sql</c> is the deployment mistake this catches.
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
