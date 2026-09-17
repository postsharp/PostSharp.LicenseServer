namespace SharpCrafters.Backstage.LicenseServer;

/// <summary>
/// A point on the usage timeline of a license: the moment a lease starts or ends, together with what
/// was in use just after that moment.
/// </summary>
public sealed class LeaseCountingPoint
{
    public required DateTime Time { get; init; }

    public required LeaseCountingPointKind Kind { get; init; }

    public required Lease Lease { get; init; }

    /// <summary>
    /// Gets the number of seats consumed immediately after this point.
    /// </summary>
    /// <remarks>
    /// A seat covers <c>MachinesPerUser</c> machines of one user, so a user working on more machines
    /// than that consumes more than one seat. This is the quantity the allocator compares to the
    /// capacity of the license when it decides whether to grant a lease.
    /// </remarks>
    public int LeaseCount { get; set; }

    /// <summary>
    /// Gets the number of users holding at least one lease immediately after this point.
    /// </summary>
    /// <remarks>
    /// This counts people rather than what they consume, so it is never larger than
    /// <see cref="LeaseCount"/> and is smaller whenever somebody works on enough machines to take a
    /// second seat.
    /// </remarks>
    public int UserCount { get; set; }
}
