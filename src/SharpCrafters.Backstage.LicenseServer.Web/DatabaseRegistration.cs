using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using SharpCrafters.Backstage.LicenseServer.Data;

namespace SharpCrafters.Backstage.LicenseServer;

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
    /// SQL Server is the engine supported in production. SQLite is supported so that the server can
    /// be evaluated, and so that the test suite can run the real application against a database held
    /// in memory.
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

        return provider.ToLowerInvariant() switch
        {
            "sqlserver" => services.AddDbContext<LicenseServerDbContext>(
                options => options.UseSqlServer( connectionString ) ),

            "sqlite" => services.AddDbContext<LicenseServerDbContext>(
                options => options.UseSqlite( ResolveSqliteFile( connectionString, environment ) ) ),

            _ => throw new InvalidOperationException(
                $"Unknown database provider '{provider}'. Use 'SqlServer' or 'Sqlite'." )
        };
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
