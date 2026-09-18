// Copyright (c) SharpCrafters s.r.o. See the LICENSE.md file in the root directory of this repository root for details.

using SharpCrafters.Backstage.LicenseServer.Data;

namespace SharpCrafters.Backstage.LicenseServer.Tests;

/// <summary>
/// The rounding rule that decides how many seats a group of users consumes. This arithmetic used to
/// run inside a SQL <c>GROUP BY</c> clause, where no test could reach it.
/// </summary>
public sealed class SeatCountingTests
{
    [Fact]
    public void CountSeats_NoUsers_ReturnsZero() => Assert.Equal( 0, SeatCounter.CountSeats( [], 2 ) );

    [Theory]

    // One user, N machines, two machines per seat.
    [InlineData( 1, 1 )]
    [InlineData( 2, 1 )]
    [InlineData( 3, 2 )]
    [InlineData( 4, 2 )]
    [InlineData( 5, 3 )]
    public void CountSeats_SingleUser_RoundsMachinesUp( int machines, int expectedSeats )
        => Assert.Equal( expectedSeats, SeatCounter.CountSeats( [machines], 2 ) );

    [Fact]
    public void CountSeats_TwoUsersOneMachineEach_ConsumesTwoSeats() => Assert.Equal( 2, SeatCounter.CountSeats( [1, 1], 2 ) );

    [Fact]
    public void CountSeats_TwoUsersTwoMachinesEach_ConsumesTwoSeats() => Assert.Equal( 2, SeatCounter.CountSeats( [2, 2], 2 ) );

    [Fact]
    public void CountSeats_RoundsUpPerUserNotInTotal()
    {
        // Three machines for one user and one for another is 3 seats, not ceil(4/2) == 2.
        Assert.Equal( 3, SeatCounter.CountSeats( [3, 1], 2 ) );
    }

    [Theory]
    [InlineData( 1, 3, 3 )]
    [InlineData( 3, 3, 1 )]
    [InlineData( 3, 4, 2 )]
    [InlineData( 3, 7, 3 )]
    public void CountSeats_HonoursMachinesPerUser( int machinesPerUser, int machines, int expectedSeats )
        => Assert.Equal( expectedSeats, SeatCounter.CountSeats( [machines], machinesPerUser ) );

    [Fact]
    public void CountSeats_MachinesPerUserBelowOne_Throws() => Assert.Throws<ArgumentOutOfRangeException>( () => SeatCounter.CountSeats( [1], 0 ) );
}