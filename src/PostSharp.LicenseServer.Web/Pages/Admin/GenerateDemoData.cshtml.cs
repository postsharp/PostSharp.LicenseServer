using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using PostSharp.LicenseServer.Data;
using PostSharp.LicenseServer.Services;

namespace PostSharp.LicenseServer.Pages.Admin;

/// <summary>
/// Fills the database with a plausible history of lease activity, so that the dashboard and the
/// usage graph can be looked at without waiting for real traffic.
/// </summary>
/// <remarks>
/// Only available in the Development environment. The legacy version of this page was reachable in
/// production, on a deployment whose administrative pages were unrestricted by default.
/// </remarks>
public sealed class GenerateDemoDataModel(
    ILeaseRepository repository,
    LicenseServerDbContext db,
    LeaseService leaseService,
    IHostEnvironment environment,
    TimeProvider timeProvider ) : PageModel
{
    private static readonly string[] firstNames =
    [
        "David", "Jimmy", "Carroll", "Keith", "Marsha", "Mike", "Julio", "Salvatore", "Herbert", "Gary",
        "Jesse", "Louis", "Duane", "Joan", "Thomas", "Richard", "Charles", "Paul", "Albert", "Jerry",
        "Sidney", "John", "Monica", "William", "Miguel", "Melanie", "Nida", "George", "Edmund", "Michael"
    ];

    private static readonly string[] lastNames =
    [
        "Rahm", "Smith", "Lash", "Martinez", "Bassham", "Bergeron", "Bayliss", "Nail", "Thompson", "Gentile",
        "Turner", "Stinson", "Callender", "Rudder", "Lowrey", "Bourdeau", "Vega", "Numbers", "Cooper",
        "Speier", "Johnson", "Painter", "Denton", "Grise", "Davis", "Collins", "Perez", "Young", "Wilkes"
    ];

    [BindProperty]
    [Microsoft.AspNetCore.Mvc.ModelBinding.BindNever]
    public string? Message { get; private set; }

    public bool IsAvailable => environment.IsDevelopment();

    public int UserCount { get; set; } = 20;

    public int Days { get; set; } = 90;

    public IActionResult OnGet() => this.IsAvailable ? this.Page() : this.NotFound();

    public async Task<IActionResult> OnPostAsync( int userCount, int days, CancellationToken cancellationToken )
    {
        if ( !this.IsAvailable )
        {
            return this.NotFound();
        }

        this.UserCount = Math.Clamp( userCount, 1, 200 );
        this.Days = Math.Clamp( days, 1, 365 );

        License[] licenses = await repository.Licenses
            .Where( l => l.Priority >= 0 )
            .OrderBy( l => l.Priority )
            .ToArrayAsync( cancellationToken );

        if ( licenses.Length == 0 )
        {
            this.Message = "Add a license first: there is nothing to lease against.";

            return this.Page();
        }

        // Deterministic, so that re-running the generator produces a comparable history.
        Random random = new( 20260105 );

        (string User, string[] Machines)[] users = Enumerable.Range( 0, this.UserCount )
            .Select(
                i =>
                {
                    string user =
                        $"{firstNames[i % firstNames.Length]}.{lastNames[(i * 7) % lastNames.Length]}".ToLowerInvariant();

                    string[] machines = random.Next( 3 ) == 0
                        ? [$"{user}-desktop", $"{user}-laptop"]
                        : [$"{user}-desktop"];

                    return (user, machines);
                } )
            .ToArray();

        DateTime now = timeProvider.GetUtcNow().UtcDateTime;
        DateTime start = now.Date.AddDays( -this.Days );
        int granted = 0;

        for ( int day = 0; day < this.Days; day++ )
        {
            DateTime date = start.AddDays( day );

            bool isWeekend = date.DayOfWeek is DayOfWeek.Saturday or DayOfWeek.Sunday;

            foreach ( (string user, string[] machines) in users )
            {
                // Most people do not work at the weekend, and not everybody builds every day.
                if ( random.NextDouble() > (isWeekend ? 0.1 : 0.85) )
                {
                    continue;
                }

                string machine = machines[random.Next( machines.Length )];
                DateTime time = date.AddHours( 8 + (random.NextDouble() * 9) );

                Lease? lease = await leaseService.GetLeaseAsync(
                    new Version( 2025, 1, 0 ),
                    null,
                    machine,
                    user,
                    user,
                    time,
                    [],
                    licenses,
                    cancellationToken: cancellationToken );

                if ( lease != null )
                {
                    granted++;
                }
            }

            // Saved once per simulated day rather than once per lease: the change tracker would
            // otherwise grow for the whole run and make each save slower than the last.
            await db.SaveChangesAsync( cancellationToken );
        }

        this.Message =
            $"Simulated {this.Days} days of activity for {this.UserCount} users against "
            + $"{licenses.Length} license(s); {granted} lease(s) granted.";

        return this.Page();
    }
}
