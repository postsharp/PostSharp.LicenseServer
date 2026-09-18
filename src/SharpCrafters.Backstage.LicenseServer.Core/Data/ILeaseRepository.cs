namespace SharpCrafters.Backstage.LicenseServer.Data;

/// <summary>
/// Reads and writes leases. Replaces the methods that the LINQ to SQL <c>Database</c> class carried.
/// </summary>
public interface ILeaseRepository
{
    /// <summary>
    /// Gets the leases that have not been replaced by a later lease.
    /// </summary>
    IQueryable<Lease> OpenLeases { get; }

    /// <summary>
    /// Gets every lease that was recorded, including the leases that were replaced later. These rows
    /// are the audit log.
    /// </summary>
    IQueryable<Lease> Leases { get; }

    IQueryable<License> Licenses { get; }

    /// <summary>
    /// Creates a lease for a user on a machine.
    /// </summary>
    /// <returns>
    /// The new lease, or <c>null</c> when no lease can be granted beyond <paramref name="time"/>,
    /// because the license or the grace period ends first.
    /// </returns>
    Lease? CreateLease(
        License license,
        string user,
        string machine,
        string authenticatedUserName,
        DateTime time,
        bool grace );

    /// <summary>
    /// Replaces a lease with a new one ending later.
    /// </summary>
    /// <returns>The new lease, or <c>null</c> when it cannot be extended past <paramref name="time"/>.</returns>
    Lease? ProlongLease( Lease oldLease, string authenticatedUserName, DateTime time );

    /// <summary>
    /// Ends a lease immediately, by inserting a lease that overwrites it.
    /// </summary>
    void CancelLease( Lease lease, string authenticatedUserName, DateTime time );

    /// <summary>
    /// Counts the seats of a license that are in use at a given instant.
    /// </summary>
    int GetActiveSeats( int licenseId, DateTime dateTime );

    /// <summary>
    /// Returns the usage timeline of a license during a period. The timeline is a sequence of events
    /// that open and close a lease, and each event carries the number of seats in use after it.
    /// </summary>
    IEnumerable<LeaseCountingPoint> GetLeaseCountingPoints( int licenseId, DateTime startTime, DateTime endTime );

    Task<int> SaveChangesAsync( CancellationToken cancellationToken = default );

    int SaveChanges();
}
