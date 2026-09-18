using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;

namespace SharpCrafters.Backstage.LicenseServer.Data;

/// <summary>
/// The license server database. The hosting application chooses the provider: SQL Server or
/// PostgreSQL in production, SQLite for an evaluation and for the tests.
/// </summary>
/// <remarks>
/// The model maps exactly onto the schema that <c>CreateTables.sql</c> creates, so that an existing
/// deployment is upgraded without any work on the database. The project contains no EF migration.
/// <c>SchemaCompatibilityTests</c> verifies the compatibility of the model with the schema, and a run
/// against SQL Server or PostgreSQL creates every test database from the script of that engine, which
/// verifies the same thing against a live server.
/// </remarks>
public class LicenseServerDbContext : DbContext
{
    public LicenseServerDbContext( DbContextOptions<LicenseServerDbContext> options ) : base( options ) { }

    public DbSet<License> Licenses => this.Set<License>();

    public DbSet<Lease> Leases => this.Set<Lease>();

    /// <summary>
    /// Gets the leases that no later lease has replaced, that is, the leases that are in effect.
    /// This query is the only place that interprets <see cref="Lease.OverwrittenLeaseId"/>.
    /// </summary>
    /// <remarks>
    /// The query is an anti-join. The legacy query was a left join with a test for null, and it
    /// returned a lease twice when two leases had overwritten it. No unique constraint prevents
    /// that.
    /// </remarks>
    public IQueryable<Lease> OpenLeases
        => this.Leases.Where( l => !this.Leases.Any( o => o.OverwrittenLeaseId == l.LeaseId ) );

    protected override void OnModelCreating( ModelBuilder modelBuilder )
    {
        modelBuilder.ApplyConfigurationsFromAssembly( typeof(LicenseServerDbContext).Assembly );

        bool isSqlServer = this.Database.ProviderName == "Microsoft.EntityFrameworkCore.SqlServer";

        // Every timestamp of this database is UTC, and the converter states it in both directions.
        //
        // On the way out: SQL Server returns DateTimeKind.Unspecified. Lease.Write serializes the
        // timestamps as UTC, and XmlConvert treats an Unspecified value as a local time, which shifted
        // every exported timestamp by the offset of the server.
        //
        // On the way in: PostgreSQL stores these columns as 'timestamp with time zone', and Npgsql
        // refuses a value whose kind is Unspecified. A date built from a year and a month, which is
        // what the export takes from its query string, is such a value. The kind is set and not
        // converted, so the value written is the value the caller gave, which is what SQL Server and
        // SQLite have always stored.
        ValueConverter<DateTime, DateTime> toUtc =
            new(
                v => DateTime.SpecifyKind( v, DateTimeKind.Utc ),
                v => DateTime.SpecifyKind( v, DateTimeKind.Utc ) );

        ValueConverter<DateTime?, DateTime?> toUtcNullable =
            new(
                v => v.HasValue ? DateTime.SpecifyKind( v.Value, DateTimeKind.Utc ) : null,
                v => v.HasValue ? DateTime.SpecifyKind( v.Value, DateTimeKind.Utc ) : null );

        foreach ( var property in modelBuilder.Model.GetEntityTypes().SelectMany( e => e.GetProperties() ) )
        {
            if ( property.ClrType == typeof(DateTime) )
            {
                property.SetValueConverter( toUtc );
            }
            else if ( property.ClrType == typeof(DateTime?) )
            {
                property.SetValueConverter( toUtcNullable );
            }
            else
            {
                continue;
            }

            if ( isSqlServer )
            {
                // The existing columns have the type 'datetime'. With the default type of EF,
                // 'datetime2', SQL Server would convert the column at every comparison of StartTime
                // and EndTime.
                property.SetColumnType( "datetime" );
            }
        }

        if ( isSqlServer )
        {
            modelBuilder.Entity<License>().Property( x => x.LicenseKey ).HasColumnType( "text" );
        }
        else
        {
            // The default collation of SQL Server ignores the case. The default collation of SQLite
            // and of PostgreSQL does not. Without a collation that ignores the case, grouping the
            // leases by user name would behave differently from one engine to the next, and a user
            // who signed in under two spellings would hold two sets of machines.
            //
            // SQLite carries NOCASE. PostgreSQL carries no collation that ignores the case, so the
            // schema creates one.
            string collation = this.Database.ProviderName == "Npgsql.EntityFrameworkCore.PostgreSQL"
                ? CaseInsensitiveCollation
                : "NOCASE";

            modelBuilder.Entity<Lease>().Property( x => x.UserName ).UseCollation( collation );
            modelBuilder.Entity<Lease>().Property( x => x.Machine ).UseCollation( collation );
        }
    }

    /// <summary>
    /// The name of the PostgreSQL collation that ignores the case. <c>CreateTables.PostgreSql.sql</c>
    /// creates it, and so does a test database, because PostgreSQL defines no such collation itself.
    /// </summary>
    public const string CaseInsensitiveCollation = "license_server_ci";
}
