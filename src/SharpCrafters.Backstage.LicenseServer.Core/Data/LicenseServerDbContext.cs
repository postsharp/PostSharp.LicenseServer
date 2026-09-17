using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;

namespace SharpCrafters.Backstage.LicenseServer.Data;

/// <summary>
/// The license server database. The provider is chosen by the hosting application: SQL Server in
/// production, SQLite in tests.
/// </summary>
/// <remarks>
/// The model maps onto the schema created by <c>CreateTables.sql</c> exactly, so that an existing
/// deployment upgrades without any database work. There are deliberately no EF migrations; schema
/// compatibility is guaranteed by <c>SchemaCompatibilityTests</c> instead.
/// </remarks>
public class LicenseServerDbContext( DbContextOptions<LicenseServerDbContext> options ) : DbContext( options )
{
    public DbSet<License> Licenses => this.Set<License>();

    public DbSet<Lease> Leases => this.Set<Lease>();

    /// <summary>
    /// Gets the leases that have not been replaced by a later lease, i.e. the leases that are
    /// currently in effect. This is the only place where <see cref="Lease.OverwrittenLeaseId"/> is
    /// interpreted.
    /// </summary>
    /// <remarks>
    /// Expressed as an anti-join rather than the legacy left-join-where-null, which yielded a lease
    /// twice if it had ever been overwritten twice. There is no unique constraint preventing that.
    /// </remarks>
    public IQueryable<Lease> OpenLeases
        => this.Leases.Where( l => !this.Leases.Any( o => o.OverwrittenLeaseId == l.LeaseId ) );

    protected override void OnModelCreating( ModelBuilder modelBuilder )
    {
        modelBuilder.ApplyConfigurationsFromAssembly( typeof(LicenseServerDbContext).Assembly );

        bool isSqlServer = this.Database.ProviderName == "Microsoft.EntityFrameworkCore.SqlServer";

        // SQL Server returns DateTimeKind.Unspecified. Lease.Write serializes timestamps as UTC, and
        // XmlConvert treats an Unspecified value as *local* time, which shifted every exported
        // timestamp by the server's UTC offset. Tagging the kind on materialization fixes that.
        ValueConverter<DateTime, DateTime> toUtc =
            new( v => v, v => DateTime.SpecifyKind( v, DateTimeKind.Utc ) );

        ValueConverter<DateTime?, DateTime?> toUtcNullable =
            new( v => v, v => v.HasValue ? DateTime.SpecifyKind( v.Value, DateTimeKind.Utc ) : null );

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
                // The existing columns are 'datetime'. Letting EF default to 'datetime2' would force
                // SQL Server to convert the column on every StartTime/EndTime comparison.
                property.SetColumnType( "datetime" );
            }
        }

        if ( isSqlServer )
        {
            modelBuilder.Entity<License>().Property( x => x.LicenseKey ).HasColumnType( "text" );
        }
        else
        {
            // SQL Server's default collation is case-insensitive and SQLite's is not. Without this,
            // grouping leases by user name would behave differently in tests than in production.
            modelBuilder.Entity<Lease>().Property( x => x.UserName ).UseCollation( "NOCASE" );
            modelBuilder.Entity<Lease>().Property( x => x.Machine ).UseCollation( "NOCASE" );
        }
    }
}
