using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using SharpCrafters.Backstage.LicenseServer.Data;
using SharpCrafters.Backstage.Licensing;

namespace SharpCrafters.Backstage.LicenseServer.Licensing;

/// <summary>
/// Puts license keys in the database of a development server, so that it can serve a lease without
/// anybody buying a license first.
/// </summary>
public static class TestLicenseSeeder
{
    /// <summary>
    /// The licenses that are seeded. One per product family, so that a client naming a product
    /// exercises the matching rather than falling back on "any product".
    /// </summary>
    private static readonly (int LicenseId, LicenseProduct Product, short Users)[] licenses =
    [
        (900001, LicenseProduct.MetalamaProfessional, 25),
        (900002, LicenseProduct.PostSharpUltimate, 25)
    ];

    /// <summary>
    /// Adds the license keys that are missing, and leaves the ones that are there alone.
    /// </summary>
    /// <returns>The identifiers of the licenses that were added.</returns>
    /// <remarks>
    /// Seeding is skipped for a license that is already present rather than replaced, so that a
    /// server restarted against the same database keeps the leases it has granted. The keys are
    /// signed by <see cref="TestLicenseAuthority"/>, whose key pair lives in the same data directory,
    /// so both survive a restart together or are lost together.
    /// </remarks>
    public static IReadOnlyList<int> Seed(
        LicenseServerDbContext db,
        TestLicenseAuthority authority,
        TimeProvider timeProvider,
        ILogger logger )
    {
        ArgumentNullException.ThrowIfNull( db );
        ArgumentNullException.ThrowIfNull( authority );
        ArgumentNullException.ThrowIfNull( timeProvider );
        ArgumentNullException.ThrowIfNull( logger );

        DateTime now = timeProvider.GetUtcNow().UtcDateTime;
        HashSet<int> existing = [.. db.Licenses.Select( l => l.LicenseId )];
        List<int> added = [];

        foreach ( (int licenseId, LicenseProduct product, short users) in licenses )
        {
            if ( existing.Contains( licenseId ) )
            {
                continue;
            }

            db.Licenses.Add(
                new License
                {
                    LicenseId = licenseId,
                    ProductCode = ProductCodes.ForStorage( product ),
                    CreatedOn = now,
                    LicenseKey = authority.CreateLicenseKey(
                        licenseId,
                        product,
                        LicenseType.Business,
                        users,
                        graceDays: 5,
                        gracePercent: 20,
                        validTo: now.AddYears( 5 ) )
                } );

            added.Add( licenseId );
        }

        if ( added.Count > 0 )
        {
            db.SaveChanges();

            logger.LogWarning(
                "Added the test licenses {LicenseIds}, signed by this server's own test licensing "
                + "authority. They are not licenses anybody sold, and no other server accepts them.",
                string.Join( ", ", added ) );
        }

        return added;
    }
}
