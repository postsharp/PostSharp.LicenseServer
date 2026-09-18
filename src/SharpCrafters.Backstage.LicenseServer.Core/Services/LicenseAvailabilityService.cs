// Copyright (c) SharpCrafters s.r.o. See the LICENSE.md file in the root directory of this repository root for details.

using Microsoft.EntityFrameworkCore;
using SharpCrafters.Backstage.LicenseServer.Data;
using SharpCrafters.Backstage.LicenseServer.Licensing;

namespace SharpCrafters.Backstage.LicenseServer.Services;

/// <summary>
/// Reports whether the server can serve a lease. This is the question a monitoring system asks. The
/// list of licenses on the home page does not answer it.
/// </summary>
/// <remarks>
/// <para>
/// This question is weaker than the question <see cref="LeaseService"/> answers. A lease request
/// names a product, a version and a build date, and a license can be refused because of any of the
/// three. This service ignores the three and reports whether a license could serve a client at this
/// moment. A server that this service reports as available can therefore still deny an individual
/// request.
/// </para>
/// <para>
/// This service reads and never writes. The allocator starts the grace period of a license when it
/// grants a lease within that period. A health check that did the same would start the grace period
/// of a license that no client uses, and the period would elapse while the server is idle.
/// </para>
/// </remarks>
public sealed class LicenseAvailabilityService
{
    private readonly ILeaseRepository repository;
    private readonly ILicenseParser licenseParser;
    private readonly ILicenseServerVersion serverVersion;

    public LicenseAvailabilityService(
        ILeaseRepository repository,
        ILicenseParser licenseParser,
        ILicenseServerVersion serverVersion )
    {
        this.repository = repository;
        this.licenseParser = licenseParser;
        this.serverVersion = serverVersion;
    }

    public async Task<LicenseAvailability> GetAvailabilityAsync(
        DateTime now,
        CancellationToken cancellationToken = default )
    {
        var licenses = await this.repository.Licenses
            .AsNoTracking()
            .ToArrayAsync( cancellationToken );

        var available = 0;
        var disabled = 0;
        var invalid = 0;
        var expired = 0;
        var exhausted = 0;

        foreach ( var license in licenses )
        {
            if ( license.Priority < 0 )
            {
                disabled++;

                continue;
            }

            var parsedLicense = this.licenseParser.TryParse( license.LicenseKey );

            if ( parsedLicense == null
                 || !parsedLicense.IsLicenseServerEligible
                 || parsedLicense.MinPostSharpVersion > this.serverVersion.LicensingLibraryVersion )
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
                // A license with no seat limit always has a free seat.
                available++;

                continue;
            }

            var usage = this.repository.GetActiveSeats( license.LicenseId, now );

            if ( usage < parsedLicense.UserNumber.Value )
            {
                available++;

                continue;
            }

            // Above the capacity, only the grace period remains. It is limited by a number of seats
            // and by a number of days. A license whose grace period has not started yet starts it at
            // the next request, so it counts as available.
            var graceLimit = LicenseCapacity.GetGraceLimit( parsedLicense.UserNumber.Value, parsedLicense.GracePercent );
            var graceEnd = ( license.GraceStartTime ?? now ).AddDays( parsedLicense.GraceDays );

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
    /// Gets the number of licenses whose key the server cannot parse, whose type a license server
    /// may not serve, or that require a newer licensing library than this server contains.
    /// </summary>
    public required int Invalid { get; init; }

    /// <summary>
    /// Gets the number of licenses whose validity has ended.
    /// </summary>
    public required int Expired { get; init; }

    /// <summary>
    /// Gets the number of licenses that are full and whose grace period has ended or is full as
    /// well.
    /// </summary>
    public required int Exhausted { get; init; }

    public int Total => this.Available + this.Disabled + this.Invalid + this.Expired + this.Exhausted;

    public bool CanServeLease => this.Available > 0;

    /// <summary>
    /// Returns one sentence that says whether a lease can be served, and why not when it cannot.
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