namespace SharpCrafters.Backstage.LicenseServer;

/// <summary>
/// The kind of event a <see cref="LeaseCountingPoint"/> represents.
/// </summary>
/// <remarks>
/// The numeric values are load-bearing. Counting points are ordered by time and then by kind, so
/// <see cref="Close"/> being lower than <see cref="Open"/> makes a lease that ends at the exact
/// instant another begins release its machine before the next one claims it. Renumbering these
/// would make the seat count spike transiently and would make the close handler throw, because it
/// would no longer find the machine it is closing.
/// </remarks>
public enum LeaseCountingPointKind
{
    // Process close before open!
    Close = 1,
    Open = 2
}
