using Microsoft.EntityFrameworkCore;
using SharpCrafters.Backstage.LicenseServer.Data;
using SharpCrafters.Backstage.LicenseServer.Licensing;

namespace SharpCrafters.Backstage.LicenseServer.Services;

/// <summary>
/// Answers whether the server can serve a lease at all, which is what a monitoring system asks and
/// what the license list on the home page does not say.
/// </summary>
/// <remarks>
/// <para>
/// This is a weaker question than the one <see cref="LeaseService"/> answers. A lease request names
/// a product, a version and a build date, and a license can be refused on any of the three. This
/// service leaves all three out and reports whether a license could serve some client now, so a
/// server it calls available can still deny an individual request.
/// </para>
/// <para>
/// It reads and never writes. The allocator starts the grace period of a license when it falls back
/// on it; a health check that did the same would start the grace period of a license nobody is
/// using, and the clock of that period would run while the server sat idle.
/// </para>
/// </remarks>
public sealed class LicenseAvailabilityService(
    ILeaseRepository repository,
    ILicenseParser licenseParser,
    ILicenseServerVersion serverVersion )
{
    public async Task<LicenseAvailability> GetAvailabilityAsync(
        DateTime now,
        CancellationToken cancellationToken = default )
    {
        License[] licenses = await repository.Licenses
            .AsNoTracking()
            .ToArrayAsync( cancellationToken );

        int available = 0;
        int disabled = 0;
        int invalid = 0;
        int expired = 0;
        int exhausted = 0;

        foreach ( License license in licenses )
        {
            if ( license.Priority < 0 )
            {
                disabled++;

                continue;
            }

            LicenseInfo? parsedLicense = licenseParser.TryParse( license.LicenseKey );

            if ( parsedLicense == null
                 || !parsedLicense.IsLicenseServerEligible
                 || parsedLicense.MinPostSharpVersion > serverVersion.LicensingLibraryVersion )
            {
                invalid++;

                continue;
            }

            if ( parsedLicense.ValidTo.HasValue && parsedLicense.ValidTo.Value <= now )
            {
                expired++;

                continue;
            }

            if ( !parsedLicense.UserNumber.HasValue )
            {
                // A license with no seat limit always has room.
                available++;

                continue;
            }

            int usage = repository.GetActiveSeats( license.LicenseId, now );

            if ( usage < parsedLicense.UserNumber.Value )
            {
                available++;

                continue;
            }

            // Past capacity the grace period is what is left, and it is bounded both by a number of
            // seats and by a number of days. A license whose grace period has not started yet would
            // start it on the next request, so it counts as available.
            int graceLimit = LicenseCapacity.GetGraceLimit( parsedLicense.UserNumber.Value, parsedLicense.GracePercent );
            DateTime graceEnd = (license.GraceStartTime ?? now).AddDays( parsedLicense.GraceDays );

            if ( usage < graceLimit && graceEnd > now )
            {
                available++;
            }
            else
            {
                exhausted++;
            }
        }

        return new LicenseAvailability
        {
            Available = available,
            Disabled = disabled,
            Invalid = invalid,
            Expired = expired,
            Exhausted = exhausted
        };
    }
}

/// <summary>
/// How many licenses the server holds, and why the ones it cannot serve are unusable.
/// </summary>
public sealed record LicenseAvailability
{
    /// <summary>
    /// Gets the number of licenses that can serve a lease now.
    /// </summary>
    public required int Available { get; init; }

    /// <summary>
    /// Gets the number of licenses an administrator has disabled.
    /// </summary>
    public required int Disabled { get; init; }

    /// <summary>
    /// Gets the number of license keys the server cannot parse, cannot serve at all, or that require
    /// a newer licensing library than this server carries.
    /// </summary>
    public required int Invalid { get; init; }

    /// <summary>
    /// Gets the number of licenses whose validity has ended.
    /// </summary>
    public required int Expired { get; init; }

    /// <summary>
    /// Gets the number of licenses that are full and whose grace period is over or full as well.
    /// </summary>
    public required int Exhausted { get; init; }

    public int Total => this.Available + this.Disabled + this.Invalid + this.Expired + this.Exhausted;

    public bool CanServeLease => this.Available > 0;

    /// <summary>
    /// Returns one sentence saying whether a lease can be served, and why not when it cannot.
    /// </summary>
    public string Describe()
    {
        if ( this.CanServeLease )
        {
            return $"{this.Available} of {this.Total} license(s) can serve a lease.";
        }

        if ( this.Total == 0 )
        {
            return "No license is registered.";
        }

        List<string> reasons = [];

        Add( this.Exhausted, "at capacity" );
        Add( this.Expired, "expired" );
        Add( this.Invalid, "invalid" );
        Add( this.Disabled, "disabled" );

        return $"No license can serve a lease: {string.Join( ", ", reasons )}.";

        void Add( int count, string reason )
        {
            if ( count > 0 )
            {
                reasons.Add( $"{count} {reason}" );
            }
        }
    }
}
