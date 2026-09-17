namespace SharpCrafters.Backstage.LicenseServer.Data;

/// <summary>
/// Counts the seats that the holders of a set of leases consume.
/// </summary>
/// <remarks>
/// <para>
/// A seat is one user working on up to <c>MachinesPerUser</c> machines. A user working on more
/// machines takes more than one seat: the number of machines divided by <c>MachinesPerUser</c>,
/// rounded up. At the default of two, one or two machines are one seat and three or four are two.
/// </para>
/// <para>
/// The seat is the unit the capacity of a license key is expressed in, and the only place where the
/// number of machines enters the licensing rules. The licence agreement puts it the other way round,
/// as a number of authorized users each entitled to a number of devices; the two say the same thing,
/// and this is the form the server counts in.
/// </para>
/// </remarks>
public static class SeatCounter
{
    /// <summary>
    /// Counts the seats consumed by users working on the given numbers of machines.
    /// </summary>
    /// <param name="machinesPerUser">The number of distinct machines each user is working on.</param>
    /// <param name="machinesPerUserLimit">The number of machines one seat covers.</param>
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
