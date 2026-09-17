namespace SharpCrafters.Backstage.LicenseServer.Licensing;

/// <summary>
/// The facts the license server needs about a license key, projected out of the PostSharp SDK so
/// that the rest of the application does not depend on the SDK and can be tested without a real
/// license key.
/// </summary>
public sealed record LicenseInfo
{
    public required int LicenseId { get; init; }

    /// <summary>
    /// Gets the licensed product, as stored in the <c>ProductCode</c> column.
    /// </summary>
    public required string Product { get; init; }

    public required string LicenseType { get; init; }

    /// <summary>
    /// Gets the number of concurrent users allowed by the license, or null when unlimited.
    /// </summary>
    public int? UserNumber { get; init; }

    /// <summary>
    /// Gets the date after which the license itself stops working.
    /// </summary>
    public DateTime? ValidTo { get; init; }

    /// <summary>
    /// Gets the date after which builds of PostSharp are no longer covered by the maintenance
    /// subscription.
    /// </summary>
    public DateTime? SubscriptionEndDate { get; init; }

    /// <summary>
    /// Gets the lowest version of PostSharp that understands this license.
    /// </summary>
    public required Version MinPostSharpVersion { get; init; }

    /// <summary>
    /// Gets the number of days during which the license may be over-used before requests are denied.
    /// </summary>
    public required int GraceDays { get; init; }

    /// <summary>
    /// Gets the percentage by which <see cref="UserNumber"/> may be exceeded during the grace period.
    /// </summary>
    public required int GracePercent { get; init; }

    /// <summary>
    /// Gets a value indicating whether this kind of license may be served by a license server at all.
    /// </summary>
    public required bool IsLicenseServerEligible { get; init; }

    public string? LicenseTypeName { get; init; }

    public string? ProductName { get; init; }
}
