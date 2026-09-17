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
    /// Gets the number of seats consumed immediately after this point.
    /// </summary>
    public int LeaseCount { get; set; }
}
