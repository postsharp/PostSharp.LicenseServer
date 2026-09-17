using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using SharpCrafters.Backstage.LicenseServer.Data;
using SharpCrafters.Backstage.LicenseServer.Licensing;

namespace SharpCrafters.Backstage.LicenseServer.Pages;

/// <summary>
/// The dashboard: every registered license, with how much of it is in use right now.
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
                        CurrentUsers = repository.GetActiveLeads( license.LicenseId, now ),
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
        /// Gets the modifier that colours the status: green for an active license, orange for a key
        /// that cannot be parsed, amber while the grace period runs.
        /// </summary>
        public string StatusModifier
            => this.LicenseType == "INVALID" ? "invalid"
                : this.GraceStartTime != null ? "grace"
                : this.Status == "Active" ? "active"
                : "disabled";
    }
}
