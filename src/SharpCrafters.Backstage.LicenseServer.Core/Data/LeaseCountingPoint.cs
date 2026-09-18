// Copyright (c) SharpCrafters s.r.o. See the LICENSE.md file in the root directory of this repository root for details.

namespace SharpCrafters.Backstage.LicenseServer;

/// <summary>
/// A point of the usage timeline of a license. It is the instant at which a lease starts or ends,
/// with the number of seats in use immediately after that instant.
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
    /// A seat is one user and the machines that user works on, up to <c>MachinesPerUser</c>
    /// machines. See <see cref="Data.SeatCounter"/>. The allocator compares this quantity to the
    /// capacity of the license when it decides whether to grant a lease, and the usage chart draws
    /// the same quantity against the capacity.
    /// </remarks>
    public int SeatCount { get; set; }
}