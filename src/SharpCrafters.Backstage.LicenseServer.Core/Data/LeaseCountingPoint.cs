namespace SharpCrafters.Backstage.LicenseServer;

/// <summary>
/// A point on the usage timeline of a license: the moment a lease starts or ends, together with the
/// number of seats in use just after that moment.
/// </summary>
public sealed class LeaseCountingPoint
{
    public required DateTime Time { get; init; }

    public required LeaseCountingPointKind Kind { get; init; }

    public required Lease Lease { get; init; }

    /// <summary>
    /// Gets the number of seats in use immediately after this point.
    /// </summary>
    /// <remarks>
    /// A seat is one user and the machines that user works on, up to <c>MachinesPerUser</c> of them;
    /// see <see cref="Data.SeatCounter"/>. This is the quantity the allocator compares to the
    /// capacity of the license when it decides whether to grant a lease, so it is also what the usage
    /// chart draws against the capacity.
    /// </remarks>
    public int SeatCount { get; set; }
}
