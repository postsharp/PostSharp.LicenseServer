namespace SharpCrafters.Backstage.LicenseServer.Licensing;

/// <summary>
/// The properties of a license key that the license server needs. The licensing library provides
/// them, and the rest of the application uses this type, so that it does not depend on that library
/// and can be tested without a real license key.
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
    /// Gets the number of concurrent users that the license allows, or null when the license sets no
    /// limit.
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
    /// Gets the lowest version of PostSharp that can read this license.
    /// </summary>
    public required Version MinPostSharpVersion { get; init; }

    /// <summary>
    /// Gets the lowest version of Metalama that can read this license, or null when every version of
    /// Metalama can read it. The signature algorithm of the license key decides the value: a key
    /// signed with an elliptic curve is read by no version released before that algorithm.
    /// </summary>
    public Version? MinMetalamaVersion { get; init; }

    /// <summary>
    /// Gets a value indicating whether <see cref="Product"/> is a Metalama product. A Metalama client
    /// reads <see cref="MinMetalamaVersion"/> and a PostSharp client reads
    /// <see cref="MinPostSharpVersion"/>, and the two are independent of each other.
    /// </summary>
    public bool IsMetalamaProduct { get; init; }

    /// <summary>
    /// Gets the lowest version of the client that can read this license, which is the minimum of the
    /// family of <see cref="Product"/>.
    /// </summary>
    public Version MinClientVersion
        => this.IsMetalamaProduct
            ? this.MinMetalamaVersion ?? new Version( 0, 0, 0 )
            : this.MinPostSharpVersion;

    /// <summary>
    /// Gets the name of the product family, which the messages that name a version use.
    /// </summary>
    public string ClientName => this.IsMetalamaProduct ? "Metalama" : "PostSharp";

    /// <summary>
    /// Gets the number of days during which the license may be used above its capacity before the
    /// server denies a request.
    /// </summary>
    public required int GraceDays { get; init; }

    /// <summary>
    /// Gets the percentage by which <see cref="UserNumber"/> may be exceeded during the grace period.
    /// </summary>
    public required int GracePercent { get; init; }

    /// <summary>
    /// Gets a value indicating whether a license server may serve this kind of license.
    /// </summary>
    public required bool IsLicenseServerEligible { get; init; }

    public string? LicenseTypeName { get; init; }

    public string? ProductName { get; init; }
}
