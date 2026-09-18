using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using SharpCrafters.Backstage.LicenseServer.Data;
using SharpCrafters.Backstage.LicenseServer.Licensing;

namespace SharpCrafters.Backstage.LicenseServer.Pages;

/// <summary>
/// The home page. It lists every registered license with the part of its capacity that is in use.
/// </summary>
public sealed class IndexModel(
    ILeaseRepository repository,
    ILicenseParser licenseParser,
    TimeProvider timeProvider ) : PageModel
{
    public IReadOnlyList<LicenseSummary> Licenses { get; private set; } = [];

    public async Task OnGetAsync( CancellationToken cancellationToken )
    {
        License[] licenses = await repository.Licenses
            .OrderBy( l => l.Priority )
            .ThenByDescending( l => l.LicenseId )
            .AsNoTracking()
            .ToArrayAsync( cancellationToken );

        DateTime now = timeProvider.GetUtcNow().UtcDateTime;
        List<LicenseSummary> summaries = [];

        foreach ( License license in licenses )
        {
            LicenseInfo? parsedLicense = licenseParser.TryParse( license.LicenseKey );

            summaries.Add(
                parsedLicense == null
                    ? new LicenseSummary
                    {
                        LicenseId = license.LicenseId,
                        LicenseType = "INVALID",
                        Status = "Invalid"
                    }
                    : new LicenseSummary
                    {
                        LicenseId = license.LicenseId,
                        LicenseType = parsedLicense.LicenseType,
                        ProductCode = parsedLicense.Product,
                        MaxUsers = parsedLicense.UserNumber,
                        CurrentUsers = repository.GetActiveSeats( license.LicenseId, now ),
                        GraceStartTime = license.GraceStartTime,
                        Status = license.Priority >= 0 ? "Active" : "Disabled",
                        MaintenanceEndDate = parsedLicense.SubscriptionEndDate
                    } );
        }

        this.Licenses = summaries;
    }

    public sealed class LicenseSummary
    {
        public int LicenseId { get; init; }

        public string LicenseType { get; init; } = "";

        public string? ProductCode { get; init; }

        public int? MaxUsers { get; init; }

        public int CurrentUsers { get; init; }

        public DateTime? GraceStartTime { get; init; }

        public string? Status { get; init; }

        public DateTime? MaintenanceEndDate { get; init; }

        /// <summary>
        /// Gets the modifier that gives the status its color. An active license is green, a key that
        /// cannot be parsed is orange, and a license in its grace period is amber.
        /// </summary>
        public string StatusModifier
            => this.LicenseType == "INVALID" ? "invalid"
                : this.GraceStartTime != null ? "grace"
                : this.Status == "Active" ? "active"
                : "disabled";
    }
}
