using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using SharpCrafters.Backstage.LicenseServer.Licensing;
using SharpCrafters.Backstage.LicenseServer.Options;

namespace SharpCrafters.Backstage.LicenseServer.Data;

/// <inheritdoc cref="ILeaseRepository"/>
public sealed class LeaseRepository(
    LicenseServerDbContext db,
    IOptions<LicenseServerOptions> options,
    ILicenseParser licenseParser ) : ILeaseRepository
{
    private readonly LicenseServerOptions settings = options.Value;

    public IQueryable<Lease> OpenLeases => db.OpenLeases;

    public IQueryable<Lease> Leases => db.Leases;

    public IQueryable<License> Licenses => db.Licenses;

    public Lease? CreateLease(
        License license,
        string user,
        string machine,
        string authenticatedUserName,
        DateTime time,
        bool grace )
    {
        Lease lease = new()
        {
            License = license,

            // Assigned next to the navigation property, and not left to the fixup of EF Core, so that
            // the lease is complete before it is tracked and signed.
            LicenseId = license.LicenseId,

            AuthenticatedUser = authenticatedUserName,
            EndTime = time.AddDays( this.settings.NewLeaseDays ),
            Machine = machine,
            StartTime = time,
            UserName = user,
            Grace = grace
        };

        if ( !this.FixLease( lease, time ) )
        {
            return null;
        }

        db.Leases.Add( lease );

        return lease;
    }

    public Lease? ProlongLease( Lease oldLease, string authenticatedUserName, DateTime time )
    {
        Lease newLease = new()
        {
            License = oldLease.License,
            LicenseId = oldLease.LicenseId,
            AuthenticatedUser = authenticatedUserName,
            OverwritesLease = oldLease,
            OverwrittenLeaseId = oldLease.LeaseId,
            StartTime = oldLease.StartTime,
            EndTime = time.AddDays( this.settings.NewLeaseDays ),
            Machine = oldLease.Machine,
            UserName = oldLease.UserName,
            Grace = oldLease.Grace
        };

        if ( !this.FixLease( newLease, time ) )
        {
            return null;
        }

        db.Leases.Add( newLease );

        return newLease;
    }

    public void CancelLease( Lease lease, string authenticatedUserName, DateTime time )
    {
        Lease overwrite = new()
        {
            AuthenticatedUser = authenticatedUserName,
            License = lease.License,
            LicenseId = lease.LicenseId,
            UserName = lease.UserName,
            Machine = lease.Machine,
            StartTime = lease.StartTime,
            OverwritesLease = lease,
            OverwrittenLeaseId = lease.LeaseId,
            Grace = lease.Grace,
            EndTime = time
        };

        // A cancellation skips the adjustment of the end time. It ends the lease at the current
        // instant, and the rule that a lease must end after the current instant would reject that.
        this.FixLease( overwrite, time, false );

        db.Leases.Add( overwrite );
    }

    /// <summary>
    /// Limits the end of a lease to the end of the license and to the end of the grace period.
    /// </summary>
    /// <returns><c>false</c> when no time is left to grant.</returns>
    private bool FixLease( Lease lease, DateTime time, bool fixEndTime = true )
    {
        LicenseInfo? parsedLicense = licenseParser.TryParse( lease.License.LicenseKey );

        if ( parsedLicense == null )
        {
            throw new InvalidOperationException( $"The license key #{lease.License.LicenseId} cannot be parsed." );
        }

        if ( lease.EndTime <= lease.StartTime )
        {
            throw new InvalidOperationException( "A lease cannot end before it starts." );
        }

        if ( fixEndTime )
        {
            if ( parsedLicense.ValidTo.HasValue && parsedLicense.ValidTo < lease.EndTime )
            {
                lease.EndTime = parsedLicense.ValidTo.Value;
            }

            if ( lease.Grace )
            {
                DateTime graceEnd = lease.License.GraceStartTime!.Value.AddDays( parsedLicense.GraceDays );

                if ( lease.EndTime > graceEnd )
                {
                    lease.EndTime = graceEnd;
                }
            }

            if ( lease.EndTime <= time )
            {
                return false;
            }

            if ( lease.EndTime <= lease.StartTime )
            {
                throw new InvalidOperationException( "A lease cannot end before it starts." );
            }
        }

        // The signature is applied by SaveChanges, once the database has assigned an identifier.
        return true;
    }

    public int GetActiveSeats( int licenseId, DateTime dateTime )
    {
        // The database counts the machines and the seat arithmetic runs here, so that the query
        // translates on every provider. The query returns one row per user that holds a lease on this
        // license.
        //
        // It counts distinct machines and not leases. A user can hold two leases on one machine,
        // which happens when the clock of the server moves backwards. Counting the leases would
        // charge that user for a machine they do not work on.
        List<int> machinesPerUser = db.OpenLeases
            .Where( l => l.LicenseId == licenseId && l.StartTime <= dateTime && l.EndTime > dateTime )
            .GroupBy( l => l.UserName )
            .Select( g => g.Select( l => l.Machine ).Distinct().Count() )
            .ToList();

        return SeatCounter.CountSeats( machinesPerUser, this.settings.MachinesPerUser );
    }

    public IEnumerable<LeaseCountingPoint> GetLeaseCountingPoints(
        int licenseId,
        DateTime startTime,
        DateTime endTime )
    {
        List<Lease> leases = db.OpenLeases
            .Where( l => l.LicenseId == licenseId && l.StartTime <= endTime && l.EndTime > startTime )
            .AsNoTracking()
            .ToList();

        // A lease cancelled at the instant it was granted covers no time and belongs on no timeline.
        // It is removed before the points are built. Close sorts before Open at the same instant, so
        // its closing point would be processed first, and its opening point would then raise the
        // count for the rest of the window.
        leases.RemoveAll( l => l.EndTime <= l.StartTime );

        // Ordering in memory gives a stable sort, so the timeline is reproducible. Close sorts
        // before Open at the same instant; see LeaseCountingPointKind.
        List<LeaseCountingPoint> allRecords = leases
            .Select( l => new LeaseCountingPoint { Time = l.StartTime, Kind = LeaseCountingPointKind.Open, Lease = l } )
            .Concat(
                leases.Select(
                    l => new LeaseCountingPoint { Time = l.EndTime, Kind = LeaseCountingPointKind.Close, Lease = l } ) )
            .OrderBy( p => p.Time )
            .ThenBy( p => p.Kind )
            .ThenBy( p => p.Lease.LeaseId )
            .ToList();

        // The number of open leases that each user holds on each machine. A seat is counted from the
        // number of distinct machines, so a user who holds two leases on one machine occupies the
        // same seat as a user who holds one lease. A count per machine, instead of a list of
        // machines, makes the closing points balance the opening points for any data. A list removed
        // the machine at the first close and found nothing to remove at the second.
        Dictionary<string, Dictionary<string, int>> currentUsers = new( StringComparer.OrdinalIgnoreCase );

        int seatCount = 0;

        foreach ( LeaseCountingPoint record in allRecords )
        {
            if ( !currentUsers.TryGetValue( record.Lease.UserName, out Dictionary<string, int>? machines ) )
            {
                machines = new Dictionary<string, int>( StringComparer.OrdinalIgnoreCase );
                currentUsers.Add( record.Lease.UserName, machines );
            }

            int seatsBefore = SeatCounter.CountSeats( [machines.Count], this.settings.MachinesPerUser );
            string machine = record.Lease.Machine;

            if ( record.Kind == LeaseCountingPointKind.Open )
            {
                machines[machine] = machines.GetValueOrDefault( machine ) + 1;
            }
            else if ( machines.TryGetValue( machine, out int openLeases ) )
            {
                // The machine leaves the list when its last lease closes.
                if ( openLeases > 1 )
                {
                    machines[machine] = openLeases - 1;
                }
                else
                {
                    machines.Remove( machine );
                }
            }

            // A closing point that finds no machine to close is ignored. Both points are produced for
            // every lease that remains, and an opening point always sorts before its own closing
            // point, so this case does not occur. It is ignored and not reported as an exception,
            // because the page that draws the timeline is a report: an administrator who looks at
            // usage must not receive an error.

            int seatsAfter = SeatCounter.CountSeats( [machines.Count], this.settings.MachinesPerUser );

            seatCount += seatsAfter - seatsBefore;
            record.SeatCount = seatCount;

            yield return record;
        }
    }

    /// <summary>
    /// Saves the unit of work.
    /// </summary>
    public Task<int> SaveChangesAsync( CancellationToken cancellationToken = default )
        => db.SaveChangesAsync( cancellationToken );

    public int SaveChanges() => db.SaveChanges();
}
