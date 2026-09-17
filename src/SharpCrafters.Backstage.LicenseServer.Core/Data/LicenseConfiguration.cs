using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace SharpCrafters.Backstage.LicenseServer.Data;

/// <summary>
/// Maps <see cref="License"/> onto the <c>dbo.Licenses</c> table created by <c>CreateTables.sql</c>.
/// </summary>
public sealed class LicenseConfiguration : IEntityTypeConfiguration<License>
{
    public void Configure( EntityTypeBuilder<License> builder )
    {
        builder.ToTable( "Licenses" );
        builder.HasKey( x => x.LicenseId ).HasName( "PK_Licenses" );

        // The identifier comes from the license key, not from the database.
        builder.Property( x => x.LicenseId ).ValueGeneratedNever();

        // Mapped to SQL 'text' by LicenseServerDbContext. Never filter, sort, group or apply
        // DISTINCT on this column: T-SQL forbids 'text' in those positions and fails at run time.
        builder.Property( x => x.LicenseKey ).IsRequired().IsUnicode( false );

        builder.Property( x => x.ProductCode ).IsRequired().IsUnicode( false ).HasMaxLength( 50 );
        builder.Property( x => x.Priority ).IsRequired();
        builder.Property( x => x.CreatedOn ).IsRequired();
    }
}
