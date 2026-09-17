using Microsoft.EntityFrameworkCore;
using PostSharp.LicenseServer.Data;

namespace PostSharp.LicenseServer;

/// <summary>
/// Chooses the database engine the license server runs against.
/// </summary>
public static class DatabaseRegistration
{
    public const string ConnectionStringName = "SharpCrafters_LicenseServerConnectionString";

    /// <summary>
    /// Registers the database context against the engine named by <c>LicenseServer:DatabaseProvider</c>.
    /// </summary>
    /// <remarks>
    /// SQL Server is the supported engine for a production installation. SQLite is offered so that
    /// the server can be evaluated, and so that the test suite can run the real application against
    /// a database held in memory.
    /// </remarks>
    public static IServiceCollection AddLicenseServerDatabase(
        this IServiceCollection services,
        IConfiguration configuration )
    {
        string provider = configuration["LicenseServer:DatabaseProvider"] ?? "SqlServer";
        string? connectionString = configuration.GetConnectionString( ConnectionStringName );

        if ( string.IsNullOrWhiteSpace( connectionString ) )
        {
            throw new InvalidOperationException(
                $"The connection string '{ConnectionStringName}' is not configured." );
        }

        return provider.ToLowerInvariant() switch
        {
            "sqlserver" => services.AddDbContext<LicenseServerDbContext>(
                options => options.UseSqlServer( connectionString ) ),

            "sqlite" => services.AddDbContext<LicenseServerDbContext>(
                options => options.UseSqlite( connectionString ) ),

            _ => throw new InvalidOperationException(
                $"Unknown database provider '{provider}'. Use 'SqlServer' or 'Sqlite'." )
        };
    }
}
