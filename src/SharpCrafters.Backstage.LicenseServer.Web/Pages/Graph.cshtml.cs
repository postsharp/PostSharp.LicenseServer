using System.Globalization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using SharpCrafters.Backstage.LicenseServer.Data;
using SharpCrafters.Backstage.LicenseServer.Licensing;

namespace SharpCrafters.Backstage.LicenseServer.Pages;

/// <summary>
/// The usage history of one license. It reports the number of seats in use on each of the last days,
/// with the capacity of the license and the capacity of its grace period.
/// </summary>
/// <remarks>
/// A seat is one user and the machines that user works on, up to <c>MachinesPerUser</c> machines.
/// See <see cref="Data.SeatCounter"/>. The chart draws the quantity that the allocator compares to
/// the capacity, so the line and the two limits above it use the same unit, and the chart agrees
/// with the column "In use" of the license list.
/// </remarks>
public sealed class GraphModel(
    ILeaseRepository repository,
    ILicenseParser licenseParser,
    TimeProvider timeProvider ) : PageModel
{
    /// <summary>
    /// The windows that the page offers. The list is closed, so that an arbitrary value cannot
    /// produce an unbounded query.
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

            // The arithmetic of the allocator, which rounds up. An integer division would round
            // down, and a license of one seat with a grace period of 20 per cent would then be drawn
            // with a limit of one seat, while the server grants two.
            graceMaximum = (int) Math.Ceiling( maximum.Value * (100.0 + parsedLicense.GracePercent) / 100.0 );
            axisMaximum = graceMaximum.Value;
        }

        var dailyUsage = repository.GetLeaseCountingPoints( this.Id, startDate, endDate )
            .GroupBy( point => point.Time.Date )
            .Select(
                day => new
                {
                    Date = day.Key,
                    Peak = day.Max( point => point.SeatCount ),

                    // The timeline is ordered, and the grouping preserves that order inside a group,
                    // so the last point of a day carries the count that the next day starts from.
                    AtEndOfDay = day.Last().SeatCount
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

        // A day without lease activity keeps the count of the end of the previous day.
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
            Seats = values,
            Maximum = maximum,
            Grace = graceMaximum,
            AxisMaximum = (int) Math.Ceiling( Math.Ceiling( axisMaximum * 1.2 ) / 10 ) * 10
        };

        return this.Page();
    }

    /// <summary>
    /// The data passed to the chart script, serialized as JSON.
    /// </summary>
    public sealed class UsageChart
    {
        public string[] Labels { get; init; } = [];

        /// <summary>
        /// Gets the number of seats in use on each day of the window.
        /// </summary>
        public int[] Seats { get; init; } = [];

        /// <summary>
        /// Gets the number of seats the license allows, or null when it is unlimited.
        /// </summary>
        public int? Maximum { get; init; }

        /// <summary>
        /// Gets the number of seats allowed during the grace period.
        /// </summary>
        public int? Grace { get; init; }

        public int AxisMaximum { get; init; }
    }
}
