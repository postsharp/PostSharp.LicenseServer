using System.Globalization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using SharpCrafters.Backstage.LicenseServer.Data;
using SharpCrafters.Backstage.LicenseServer.Licensing;

namespace SharpCrafters.Backstage.LicenseServer.Pages;

/// <summary>
/// The usage history of one license: how many users held a lease on each of the last N days, against
/// the capacity of the license and its grace allowance.
/// </summary>
/// <remarks>
/// The chart counts users and not the seats they consume. The two are the same until somebody works
/// on more machines than one seat covers, and the allocator charges that person a second seat while
/// the chart still draws one person, so a license can be at its capacity with the chart below the
/// line. <see cref="LeaseCountingPoint.LeaseCount"/> carries the seats for a caller that needs them.
/// </remarks>
public sealed class GraphModel(
    ILeaseRepository repository,
    ILicenseParser licenseParser,
    TimeProvider timeProvider ) : PageModel
{
    /// <summary>
    /// The windows offered by the page. Restricting them keeps an arbitrary value from turning into
    /// an unbounded query.
    /// </summary>
    public static readonly int[] AllowedWindows = [30, 90, 180, 365];

    [BindProperty( SupportsGet = true )]
    public int Id { get; set; }

    [BindProperty( SupportsGet = true, Name = "days" )]
    public int Days { get; set; } = 30;

    public UsageChart Chart { get; private set; } = new();

    public async Task<IActionResult> OnGetAsync( CancellationToken cancellationToken )
    {
        if ( !AllowedWindows.Contains( this.Days ) )
        {
            return this.BadRequest( $"The window must be one of {string.Join( ", ", AllowedWindows )} days." );
        }

        License? license = await repository.Licenses
            .AsNoTracking()
            .SingleOrDefaultAsync( l => l.LicenseId == this.Id, cancellationToken );

        if ( license == null )
        {
            return this.NotFound();
        }

        DateTime endDate = timeProvider.GetUtcNow().UtcDateTime.Date.AddDays( 1 );
        DateTime startDate = endDate.AddDays( -this.Days );

        int? maximum = null;
        int? graceMaximum = null;
        int axisMaximum = 0;

        LicenseInfo? parsedLicense = licenseParser.TryParse( license.LicenseKey );

        if ( parsedLicense?.UserNumber != null )
        {
            maximum = parsedLicense.UserNumber;

            // The same arithmetic the allocator uses, rounding up. Integer division would floor it,
            // so a one-seat license with 20% grace would be drawn as allowing one seat while the
            // server actually grants two.
            graceMaximum = (int) Math.Ceiling( maximum.Value * (100.0 + parsedLicense.GracePercent) / 100.0 );
            axisMaximum = graceMaximum.Value;
        }

        var dailyUsage = repository.GetLeaseCountingPoints( this.Id, startDate, endDate )
            .GroupBy( point => point.Time.Date )
            .Select(
                day => new
                {
                    Date = day.Key,

                    // The number of people holding a lease, not the number of seats they consume.
                    // The two differ for a user working on more machines than one seat covers; the
                    // capacity of a license is expressed in users, which is what the two reference
                    // lines of the chart draw.
                    Peak = day.Max( point => point.UserCount ),

                    // The timeline is ordered, and grouping preserves that order within a group, so
                    // the last point of a day is the count the next day starts from.
                    AtEndOfDay = day.Last().UserCount
                } )
            .ToList();

        string[] labels = new string[this.Days];
        int[] values = new int[this.Days];
        bool[] hasValue = new bool[this.Days];
        int?[] endOfDayValues = new int?[this.Days];

        foreach ( var point in dailyUsage )
        {
            int day = (int) Math.Floor( point.Date.Subtract( startDate ).TotalDays );

            if ( point.Peak > axisMaximum )
            {
                axisMaximum = point.Peak;
            }

            // A lease that started before the window contributes to its first day.
            int index = day < 0 ? 0 : day;

            if ( index < this.Days )
            {
                values[index] = point.Peak;
                hasValue[index] = true;
                endOfDayValues[index] = point.AtEndOfDay;
            }
        }

        // Days with no lease activity inherit the count the previous day ended on.
        int lastValue = 0;

        for ( int i = 0; i < this.Days; i++ )
        {
            DateTime date = startDate.AddDays( i );

            labels[i] = date.ToString( "yyyy-MM-dd", CultureInfo.InvariantCulture );

            if ( !hasValue[i] )
            {
                values[i] = lastValue;
            }

            if ( endOfDayValues[i] != null )
            {
                lastValue = endOfDayValues[i]!.Value;
            }
        }

        this.Chart = new UsageChart
        {
            Labels = labels,
            Users = values,
            Maximum = maximum,
            Grace = graceMaximum,
            AxisMaximum = (int) Math.Ceiling( Math.Ceiling( axisMaximum * 1.2 ) / 10 ) * 10
        };

        return this.Page();
    }

    /// <summary>
    /// The data handed to the chart script, serialized as JSON.
    /// </summary>
    public sealed class UsageChart
    {
        public string[] Labels { get; init; } = [];

        /// <summary>
        /// Gets the number of users holding a lease on each day of the window.
        /// </summary>
        public int[] Users { get; init; } = [];

        /// <summary>
        /// Gets the number of concurrent users the license allows, or null when it is unlimited.
        /// </summary>
        public int? Maximum { get; init; }

        /// <summary>
        /// Gets the number of concurrent users tolerated during the grace period.
        /// </summary>
        public int? Grace { get; init; }

        public int AxisMaximum { get; init; }
    }
}
