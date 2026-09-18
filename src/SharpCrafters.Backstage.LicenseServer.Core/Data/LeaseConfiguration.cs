// Copyright (c) SharpCrafters s.r.o. See the LICENSE.md file in the root directory of this repository root for details.

using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace SharpCrafters.Backstage.LicenseServer.Data;

/// <summary>
/// Maps <see cref="Lease"/> onto the <c>dbo.Leases</c> table created by <c>CreateTables.sql</c>.
/// </summary>
public sealed class LeaseConfiguration : IEntityTypeConfiguration<Lease>
{
    public void Configure( EntityTypeBuilder<Lease> builder )
    {
        builder.ToTable( "Leases" );
        builder.HasKey( x => x.LeaseId ).HasName( "PK_Leases" );
        builder.Property( x => x.LeaseId ).ValueGeneratedOnAdd();

        builder.Property( x => x.UserName ).IsRequired().HasMaxLength( 200 );
        builder.Property( x => x.Machine ).IsRequired().HasMaxLength( 200 );
        builder.Property( x => x.AuthenticatedUser ).IsRequired().HasMaxLength( 200 );
        builder.Property( x => x.Grace ).IsRequired();

        builder.HasOne( x => x.License )
            .WithMany( x => x.Leases )
            .HasForeignKey( x => x.LicenseId )
            .HasConstraintName( "FK_Leases_Licenses" )
            .OnDelete( DeleteBehavior.Restrict );

        builder.HasOne( x => x.OverwritesLease )
            .WithMany( x => x.OverwrittenByLease )
            .HasForeignKey( x => x.OverwrittenLeaseId )
            .HasConstraintName( "FK_Leases_Leases" )
            .OnDelete( DeleteBehavior.Restrict );

        builder.HasIndex( x => x.EndTime ).HasDatabaseName( "IX_Leases_EndTime" );
        builder.HasIndex( x => x.OverwrittenLeaseId ).HasDatabaseName( "IX_Leases_OverwrittenLeaseId" );
    }
}