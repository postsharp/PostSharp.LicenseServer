using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using Microsoft.Extensions.Options;
using SharpCrafters.Backstage.LicenseServer.Licensing;
using SharpCrafters.Backstage.LicenseServer.Options;
using SharpCrafters.Backstage.LicenseServer.Security;

namespace SharpCrafters.Backstage.LicenseServer.Data;

/// <inheritdoc cref="ILeaseRepository"/>
public sealed class LeaseRepository(
    LicenseServerDbContext db,
    IOptions<LicenseServerOptions> options,
    ILicenseParser licenseParser,
    ILeaseSigner signer ) : ILeaseRepository
{
    private readonly LicenseServerOptions settings = options.Value;

    public IQueryable<Lease> OpenLeases => db.OpenLeases;

    public IQueryable<Lease> Leases => db.Leases;

    public IQueryable<License> Licenses => db.Licenses;

    /// <summary>
    /// Signs one lease, chaining it to the signature of the lease before it.
    /// </summary>
    /// <remarks>
    /// The payload is the previous signature, a semicolon, and the lease's own audit line. Signing
    /// the line that is actually exported is what lets an auditor recompute the chain from an
    /// exported file.
    /// </remarks>
    public string ComputeSignature( string? previousSignature, Lease lease )
        => signer.Sign( (previousSignature ?? string.Empty) + ";" + lease.ToAuditLine( false ) );

    /// <summary>
    /// Signs the leases that have just been inserted, in the order the database assigned them.
    /// </summary>
    /// <remarks>
    /// Leases are signed after they are inserted rather than before, because the audit line starts
    /// with the lease identifier and the database is what assigns it. Signing beforehand would put a
    /// zero in every payload, so a signature could not be recomputed from an exported line, and
    /// leases saved together would all chain from the same predecessor instead of from each other --
    /// which would let one of them be removed without breaking any later signature.
    /// </remarks>
    private void SignInsertedLeases( IReadOnlyList<Lease> inserted )
    {
        if ( inserted.Count == 0 )
        {
            return;
        }

        int firstInsertedId = inserted.Min( l => l.LeaseId );

        string? previousSignature = db.Leases
            .AsNoTracking()
            .Where( l => l.LeaseId < firstInsertedId )
            .OrderByDescending( l => l.LeaseId )
            .Select( l => l.HMAC )
            .FirstOrDefault();

        foreach ( Lease lease in inserted.OrderBy( l => l.LeaseId ) )
        {
            previousSignature = this.ComputeSignature( previousSignature, lease );
            lease.HMAC = previousSignature;
        }
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

            // Assigned alongside the navigation property rather than left to EF Core's fixup, so
            // that the lease is fully formed before it is tracked.
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

        // The signature is applied by SaveChanges, once the database has assigned an identifier.
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

    /// <summary>
    /// Saves the unit of work, signing any newly inserted leases.
    /// </summary>
    /// <remarks>
    /// The insert and the signature are two statements, so they run in one transaction: a lease must
    /// never be readable without its signature.
    /// </remarks>
    public async Task<int> SaveChangesAsync( CancellationToken cancellationToken = default )
    {
        List<Lease> inserted = this.GetPendingLeases();

        if ( inserted.Count == 0 )
        {
            return await db.SaveChangesAsync( cancellationToken );
        }

        // The caller may already have opened a transaction, as deleting a license does.
        IDbContextTransaction? transaction = db.Database.CurrentTransaction == null
            ? await db.Database.BeginTransactionAsync( cancellationToken )
            : null;

        try
        {
            int result = await db.SaveChangesAsync( cancellationToken );

            this.SignInsertedLeases( inserted );
            await db.SaveChangesAsync( cancellationToken );

            if ( transaction != null )
            {
                await transaction.CommitAsync( cancellationToken );
            }

            return result;
        }
        finally
        {
            if ( transaction != null )
            {
                await transaction.DisposeAsync();
            }
        }
    }

    public int SaveChanges()
        => this.SaveChangesAsync().GetAwaiter().GetResult();

    private List<Lease> GetPendingLeases()
        => db.ChangeTracker.Entries<Lease>()
            .Where( e => e.State == EntityState.Added )
            .Select( e => e.Entity )
            .ToList();
}
