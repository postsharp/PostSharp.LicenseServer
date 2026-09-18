using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using SharpCrafters.Backstage.LicenseServer.Data;
using SharpCrafters.Backstage.LicenseServer.Locking;
using SharpCrafters.Backstage.LicenseServer.Web.Locking;

namespace SharpCrafters.Backstage.LicenseServer;

/// <summary>
/// Chooses the database engine the license server runs against.
/// </summary>
public static class DatabaseRegistration
{
    public const string ConnectionStringName = "SharpCrafters_LicenseServerConnectionString";

    /// <summary>
    /// Registers the database context against the engine named by
    /// <c>LicenseServer:DatabaseProvider</c>, together with the lock that serializes the lease
    /// requests of that engine.
    /// </summary>
    /// <remarks>
    /// SQL Server and PostgreSQL are the engines supported in production. SQLite is supported so that
    /// the server can be evaluated and developed against without a database server, and so that the
    /// test suite can run against a database held in memory.
    /// </remarks>
    public static IServiceCollection AddLicenseServerDatabase(
        this IServiceCollection services,
        IConfiguration configuration,
        IHostEnvironment environment )
    {
        string provider = configuration["LicenseServer:DatabaseProvider"] ?? "SqlServer";
        string? connectionString = configuration.GetConnectionString( ConnectionStringName );

        if ( string.IsNullOrWhiteSpace( connectionString ) )
        {
            throw new InvalidOperationException( $"The connection string '{ConnectionStringName}' is not configured." );
        }

        // The lock belongs to the engine and not to the configuration: an administrator cannot select
        // it, and there is no implementation that serializes one process only.
        switch ( provider.ToLowerInvariant() )
        {
            case "sqlserver":
                services.AddDbContext<LicenseServerDbContext>( options => options.UseSqlServer( connectionString ) );
                services.AddScoped<ILeaseLock, SqlServerLeaseLock>();

                break;

            case "postgresql":
                services.AddDbContext<LicenseServerDbContext>( options => options.UseNpgsql( connectionString ) );
                services.AddScoped<ILeaseLock, PostgreSqlLeaseLock>();

                break;

            case "sqlite":
                services.AddDbContext<LicenseServerDbContext>(
                    options => options.UseSqlite( ResolveSqliteFile( connectionString, environment ) ) );

                services.AddScoped<ILeaseLock, SqliteLeaseLock>();

                break;

            default:
                throw new InvalidOperationException(
                    $"Unknown database provider '{provider}'. Use 'SqlServer', 'PostgreSql' or 'Sqlite'." );
        }

        return services;
    }

    /// <summary>
    /// Resolves a relative path of a SQLite file against the application directory, and not against
    /// the working directory of the process.
    /// </summary>
    private static string ResolveSqliteFile( string connectionString, IHostEnvironment environment )
    {
        SqliteConnectionStringBuilder builder = new( connectionString );

        // A database held in memory names a shared cache and not a file.
        if ( builder.Mode == SqliteOpenMode.Memory
             || string.IsNullOrEmpty( builder.DataSource )
             || builder.DataSource == ":memory:"
             || Path.IsPathRooted( builder.DataSource ) )
        {
            return connectionString;
        }

        builder.DataSource = Path.Combine( environment.ContentRootPath, builder.DataSource );

        return builder.ToString();
    }
}
