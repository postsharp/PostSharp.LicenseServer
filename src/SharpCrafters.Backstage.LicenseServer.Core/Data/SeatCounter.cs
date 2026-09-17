namespace SharpCrafters.Backstage.LicenseServer.Data;

/// <summary>
/// Converts machine counts into seat counts.
/// </summary>
public static class SeatCounter
{
    /// <summary>
    /// Counts the seats consumed by users holding the given numbers of machines. A user consumes one
    /// seat per <paramref name="machinesPerUser"/> machines, rounded up.
    /// </summary>
    /// <remarks>
    /// This arithmetic used to run inside the SQL <c>GROUP BY</c>, which no provider other than SQL
    /// Server can translate. Doing it here keeps the query portable and makes the rounding
    /// boundaries directly testable. The number of rows is bounded by the number of distinct users
    /// on one license.
    /// </remarks>
    public static int CountSeats( IEnumerable<int> machinesPerUser, int machinesPerUserLimit )
    {
        ArgumentNullException.ThrowIfNull( machinesPerUser );
        ArgumentOutOfRangeException.ThrowIfLessThan( machinesPerUserLimit, 1 );

        return (int) machinesPerUser.Sum( count => Math.Ceiling( count / (double) machinesPerUserLimit ) );
    }
}
