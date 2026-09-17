using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using PostSharp.LicenseServer.Licensing;
using PostSharp.LicenseServer.Options;
using PostSharp.LicenseServer.Security;

namespace PostSharp.LicenseServer.Data;

/// <inheritdoc cref="ILeaseRepository"/>
public sealed class LeaseRepository(
    LicenseServerDbContext db,
    IOptions<LicenseServerOptions> options,
    ILicenseParser licenseParser,
    ILeaseSigner signer ) : ILeaseRepository
{
    private readonly LicenseServerOptions settings = options.Value;

    public IQueryable<Lease> OpenLeases => db.OpenLeases;

    public IQueryable<License> Licenses => db.Licenses;

    /// <summary>
    /// Signs a lease, chaining it to the signature of the most recently persisted lease.
    /// </summary>
    /// <remarks>
    /// The query deliberately reads the database rather than the change tracker, so that leases
    /// created within one unit of work all chain from the same committed predecessor. This matches
    /// the LINQ to SQL implementation, which never flushed pending inserts before a query.
    /// </remarks>
    public string GetSignature( Lease lease )
    {
        Lease? lastLease = db.Leases
            .AsNoTracking()
            .OrderByDescending( l => l.LeaseId )
            .FirstOrDefault();

        StringWriter stringWriter = new();
        stringWriter.Write( lastLease != null ? lastLease.HMAC : "" );
        stringWriter.Write( ';' );
        lease.Write( stringWriter, false );

        return signer.Sign( stringWriter.ToString() );
    }

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

            // The foreign key is assigned explicitly because the lease is signed before it is added
            // to the change tracker, and EF Core does not populate foreign keys from navigation
            // properties until then. LINQ to SQL assigned it inside the navigation setter, so
            // omitting this would silently change what gets signed.
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

        // Cancelling deliberately skips the end-time adjustment: the point is to end the lease now,
        // which the "must end after the current moment" rule would otherwise reject.
        this.FixLease( overwrite, time, false );

        db.Leases.Add( overwrite );
    }

    /// <summary>
    /// Clamps the end of a lease to the end of the license and of the grace period, then signs it.
    /// </summary>
    /// <returns><c>false</c> when there is no time left to grant.</returns>
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

        lease.HMAC = this.GetSignature( lease );

        return true;
    }

    public int GetActiveLeads( int licenseId, DateTime dateTime )
    {
        // The seat arithmetic runs here rather than in SQL, so that the query translates on every
        // provider. The result is one row per distinct user holding a lease on this license.
        List<int> machinesPerUser = db.OpenLeases
            .Where( l => l.LicenseId == licenseId && l.StartTime <= dateTime && l.EndTime > dateTime )
            .GroupBy( l => l.UserName )
            .Select( g => g.Count() )
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

        Dictionary<string, List<string>> currentUsers = new( StringComparer.OrdinalIgnoreCase );

        int leaseCount = 0;

        foreach ( LeaseCountingPoint record in allRecords )
        {
            if ( !currentUsers.TryGetValue( record.Lease.UserName, out List<string>? machines ) )
            {
                machines = [];
                currentUsers.Add( record.Lease.UserName, machines );
            }

            int seatsBefore = SeatCounter.CountSeats( [machines.Count], this.settings.MachinesPerUser );

            if ( record.Kind == LeaseCountingPointKind.Open )
            {
                if ( !machines.Contains( record.Lease.Machine, StringComparer.OrdinalIgnoreCase ) )
                {
                    machines.Add( record.Lease.Machine );
                }
            }
            else
            {
                string machine = record.Lease.Machine;
                int index = machines.FindIndex( s => string.Equals( s, machine, StringComparison.OrdinalIgnoreCase ) );

                if ( index >= 0 )
                {
                    machines.RemoveAt( index );
                }
                else
                {
                    throw new InvalidOperationException(
                        $"Closing lease #{record.Lease.LeaseId} for machine {machine}, which is not open." );
                }
            }

            int seatsAfter = SeatCounter.CountSeats( [machines.Count], this.settings.MachinesPerUser );

            leaseCount += seatsAfter - seatsBefore;
            record.LeaseCount = leaseCount;

            yield return record;
        }
    }

    public Task<int> SaveChangesAsync( CancellationToken cancellationToken = default )
        => db.SaveChangesAsync( cancellationToken );

    public int SaveChanges() => db.SaveChanges();
}
