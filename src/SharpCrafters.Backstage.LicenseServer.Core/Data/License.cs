namespace SharpCrafters.Backstage.LicenseServer;

/// <summary>
/// A license key registered on the license server. Maps to the <c>dbo.Licenses</c> table.
/// </summary>
public class License
{
    /// <summary>
    /// Gets or sets the identifier of the license. The value comes from the license key, so the
    /// application assigns it and the database does not generate it.
    /// </summary>
    public int LicenseId { get; set; }

    public string LicenseKey { get; set; } = null!;

    public string ProductCode { get; set; } = null!;

    /// <summary>
    /// Gets or sets the order in which the server uses the licenses. A negative value disables the
    /// license.
    /// </summary>
    public int Priority { get; set; }

    public DateTime CreatedOn { get; set; }

    /// <summary>
    /// Gets or sets the instant at which the license first exceeded its capacity. That instant
    /// starts the grace period.
    /// </summary>
    public DateTime? GraceStartTime { get; set; }

    /// <summary>
    /// Gets or sets the instant at which the server sent the last warning e-mail about the grace
    /// period.
    /// </summary>
    public DateTime? GraceLastWarningTime { get; set; }

    public ICollection<Lease> Leases { get; set; } = new List<Lease>();
}
