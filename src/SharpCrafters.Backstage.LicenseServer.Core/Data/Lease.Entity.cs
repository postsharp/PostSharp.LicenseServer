namespace SharpCrafters.Backstage.LicenseServer;

/// <summary>
/// A seat of a license, held by one user on one machine for a period of time. Maps to the
/// <c>dbo.Leases</c> table.
/// </summary>
/// <remarks>
/// Leases are never updated in place. Prolonging or cancelling a lease inserts a new row that
/// overwrites the old one through <see cref="OverwrittenLeaseId"/>, which is what makes the table an
/// append-only audit log.
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
    /// Gets or sets the authenticated identity that requested the lease, which is not necessarily
    /// <see cref="UserName"/>.
    /// </summary>
    public string AuthenticatedUser { get; set; } = null!;

    /// <summary>
    /// Gets or sets the signature chaining this lease to the previous one in the audit log.
    /// </summary>
    public string? HMAC { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether this lease was granted under the grace period, i.e.
    /// beyond the capacity of the license.
    /// </summary>
    public bool Grace { get; set; }

    public License License { get; set; } = null!;

    public Lease? OverwritesLease { get; set; }

    public ICollection<Lease> OverwrittenByLease { get; set; } = new List<Lease>();
}
