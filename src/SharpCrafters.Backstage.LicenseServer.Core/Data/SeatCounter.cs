// Copyright (c) SharpCrafters s.r.o. See the LICENSE.md file in the root directory of this repository root for details.

namespace SharpCrafters.Backstage.LicenseServer.Data;

/// <summary>
/// Counts the seats that the holders of a set of leases consume.
/// </summary>
/// <remarks>
/// <para>
/// A seat is one user working on up to <c>MachinesPerUser</c> machines. A user working on more
/// machines takes more than one seat: the number of machines divided by <c>MachinesPerUser</c>,
/// rounded up. With the default value of two, one or two machines are one seat, and three or four
/// machines are two seats.
/// </para>
/// <para>
/// The capacity of a license key is expressed in seats, and this is the only rule in which the
/// number of machines appears. The license agreement expresses the same rule in the opposite
/// direction, as a number of authorized users that each may work on a number of devices. The server
/// counts in seats.
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
    /// This arithmetic used to run inside the SQL <c>GROUP BY</c> clause, which only the SQL Server
    /// provider translates. Running it here keeps the query portable, and it makes the rounding
    /// boundaries testable without a database. The number of rows is limited by the number of
    /// distinct users of one license.
    /// </remarks>
    public static int CountSeats( IEnumerable<int> machinesPerUser, int machinesPerUserLimit )
    {
        ArgumentNullException.ThrowIfNull( machinesPerUser );
        ArgumentOutOfRangeException.ThrowIfLessThan( machinesPerUserLimit, 1 );

        return (int) machinesPerUser.Sum( count => Math.Ceiling( count / (double) machinesPerUserLimit ) );
    }
}