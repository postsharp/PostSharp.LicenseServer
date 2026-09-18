// Copyright (c) SharpCrafters s.r.o. See the LICENSE.md file in the root directory of this repository root for details.

namespace SharpCrafters.Backstage.LicenseServer;

/// <summary>
/// A seat of a license, held by one user on one machine for a period of time. Maps to the
/// <c>dbo.Leases</c> table.
/// </summary>
/// <remarks>
/// The server never updates a lease. Prolonging a lease and cancelling a lease both insert a new row
/// that overwrites the previous row through <see cref="OverwrittenLeaseId"/>. The table is therefore
/// an audit log to which the server only appends.
/// </remarks>
public partial class Lease
{
    public int LeaseId { get; set; }

    /// <summary>
    /// Gets or sets the lease that this lease replaces, if any.
    /// </summary>
    public int? OverwrittenLeaseId { get; set; }

    public int LicenseId { get; set; }

    public DateTime StartTime { get; set; }

    public DateTime EndTime { get; set; }

    public string UserName { get; set; } = null!;

    public string Machine { get; set; } = null!;

    /// <summary>
    /// Gets or sets the authenticated caller that requested the lease. It can differ from
    /// <see cref="UserName"/>, which the request declares.
    /// </summary>
    public string AuthenticatedUser { get; set; } = null!;

    /// <summary>
    /// Gets or sets a value indicating whether the server granted this lease during the grace
    /// period, that is, above the capacity of the license.
    /// </summary>
    public bool Grace { get; set; }

    public License License { get; set; } = null!;

    public Lease? OverwritesLease { get; set; }

    public ICollection<Lease> OverwrittenByLease { get; set; } = new List<Lease>();
}