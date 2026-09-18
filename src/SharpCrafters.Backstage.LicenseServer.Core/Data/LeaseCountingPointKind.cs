namespace SharpCrafters.Backstage.LicenseServer;

/// <summary>
/// The kind of event a <see cref="LeaseCountingPoint"/> represents.
/// </summary>
/// <remarks>
/// The numeric values are part of the algorithm. The counting points are ordered by time and then by
/// kind. <see cref="Close"/> is lower than <see cref="Open"/>, so a lease that ends at the instant at
/// which another lease begins releases its machine before the next lease takes it. Other values
/// would raise the seat count for that instant, and the code that closes a point would no longer
/// find the machine it closes.
/// </remarks>
public enum LeaseCountingPointKind
{
    // A close is processed before an open.
    Close = 1,
    Open = 2
}
