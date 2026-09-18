// Copyright (c) SharpCrafters s.r.o. See the LICENSE.md file in the root directory of this repository root for details.

using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using SharpCrafters.Backstage.LicenseServer.Data;
using SharpCrafters.Backstage.Licensing;

namespace SharpCrafters.Backstage.LicenseServer.Licensing;

/// <summary>
/// Adds license keys to the database of a development server, so that the server can serve a lease
/// before a license is bought.
/// </summary>
public static class TestLicenseSeeder
{
    /// <summary>
    /// The licenses that this class adds. There is one license per product family, so that a client
    /// that names a product exercises the matching of the product instead of the rule that serves
    /// any product.
    /// </summary>
    private static readonly (int LicenseId, LicenseProduct Product, short Users)[] licenses =
    [
        ( 900001, LicenseProduct.MetalamaProfessional, 25 ),
        ( 900002, LicenseProduct.PostSharpUltimate, 25 )
    ];

    /// <summary>
    /// Adds the license keys that the database does not contain, and keeps the license keys it
    /// contains.
    /// </summary>
    /// <returns>The identifiers of the licenses that were added.</returns>
    /// <remarks>
    /// A license that is already present is kept and not replaced, so that a server restarted against
    /// the same database keeps the leases it has granted. <see cref="TestLicenseAuthority"/> signs
    /// the keys, and its key pair is stored in the same data directory, so the keys and the authority
    /// survive a restart together, or are lost together.
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

        var now = timeProvider.GetUtcNow().UtcDateTime;
        HashSet<int> existing = [.. db.Licenses.Select( l => l.LicenseId )];
        List<int> added = [];

        foreach ( var (licenseId, product, users) in licenses )
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