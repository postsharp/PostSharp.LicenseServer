using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;

namespace SharpCrafters.Backstage.LicenseServer.Data;

/// <summary>
/// The license server database. The provider is chosen by the hosting application: SQL Server in
/// production, SQLite in tests.
/// </summary>
/// <remarks>
/// The model maps exactly onto the schema that <c>CreateTables.sql</c> creates, so that an existing
/// deployment is upgraded without any work on the database. The project contains no EF migration.
/// <c>SchemaCompatibilityTests</c> verifies the compatibility of the model with the schema.
/// </remarks>
public class LicenseServerDbContext( DbContextOptions<LicenseServerDbContext> options ) : DbContext( options )
{
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

        // SQL Server returns DateTimeKind.Unspecified. Lease.Write serializes the timestamps as UTC,
        // and XmlConvert treats an Unspecified value as a local time, which shifted every exported
        // timestamp by the offset of the server. The converter sets the kind when a row is read.
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
            // The default collation of SQL Server ignores the case, and the default collation of
            // SQLite does not. Without this collation, grouping the leases by user name would behave
            // differently in the tests and in production.
            modelBuilder.Entity<Lease>().Property( x => x.UserName ).UseCollation( "NOCASE" );
            modelBuilder.Entity<Lease>().Property( x => x.Machine ).UseCollation( "NOCASE" );
        }
    }
}
