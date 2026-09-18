// Copyright (c) SharpCrafters s.r.o. See the LICENSE.md file in the root directory of this repository root for details.

using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using SharpCrafters.Backstage.LicenseServer.Data;
using SharpCrafters.Backstage.LicenseServer.Services;

namespace SharpCrafters.Backstage.LicenseServer.Pages.Admin;

/// <summary>
/// Fills the database with a realistic history of lease activity, so that the home page and the
/// usage graph can be examined without waiting for real requests.
/// </summary>
/// <remarks>
/// This page exists only in the Development environment. The legacy version of this page was
/// reachable in production, on a deployment whose administrative pages were open by default.
/// </remarks>
public sealed class GenerateDemoDataModel : PageModel
{
    private readonly ILeaseRepository repository;
    private readonly LicenseServerDbContext db;
    private readonly LeaseService leaseService;
    private readonly IHostEnvironment environment;
    private readonly TimeProvider timeProvider;

    public GenerateDemoDataModel(
        ILeaseRepository repository,
        LicenseServerDbContext db,
        LeaseService leaseService,
        IHostEnvironment environment,
        TimeProvider timeProvider )
    {
        this.repository = repository;
        this.db = db;
        this.leaseService = leaseService;
        this.environment = environment;
        this.timeProvider = timeProvider;
    }

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
    [BindNever]
    public string? Message { get; private set; }

    public bool IsAvailable => this.environment.IsDevelopment();

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

        var licenses = await this.repository.Licenses
            .Where( l => l.Priority >= 0 )
            .OrderBy( l => l.Priority )
            .ToArrayAsync( cancellationToken );

        if ( licenses.Length == 0 )
        {
            this.Message = "Add a license first: there is nothing to lease against.";

            return this.Page();
        }

        // A fixed seed, so that a second run of the generator produces a comparable history.
        Random random = new( 20260105 );

        (string User, string[] Machines)[] users = Enumerable.Range( 0, this.UserCount )
            .Select( i =>
            {
                var user =
                    $"{firstNames[i % firstNames.Length]}.{lastNames[( i * 7 ) % lastNames.Length]}".ToLowerInvariant();

                string[] machines = random.Next( 3 ) == 0
                    ? [$"{user}-desktop", $"{user}-laptop"]
                    : [$"{user}-desktop"];

                return ( user, machines );
            } )
            .ToArray();

        var now = this.timeProvider.GetUtcNow().UtcDateTime;
        var start = now.Date.AddDays( -this.Days );
        var granted = 0;

        for ( var day = 0; day < this.Days; day++ )
        {
            var date = start.AddDays( day );

            var isWeekend = date.DayOfWeek is DayOfWeek.Saturday or DayOfWeek.Sunday;

            foreach ( var (user, machines) in users )
            {
                // Most developers do not work at the weekend, and a developer does not build every
                // day.
                if ( random.NextDouble() > ( isWeekend ? 0.1 : 0.85 ) )
                {
                    continue;
                }

                var machine = machines[random.Next( machines.Length )];
                var time = date.AddHours( 8 + ( random.NextDouble() * 9 ) );

                var lease = await this.leaseService.GetLeaseAsync(
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

            // The generator saves once per simulated day, and not once per lease. Otherwise the
            // change tracker would grow during the whole run, and each save would be slower than the
            // previous one.
            await this.db.SaveChangesAsync( cancellationToken );
        }

        this.Message =
            $"Simulated {this.Days} days of activity for {this.UserCount} users against "
            + $"{licenses.Length} license(s); {granted} lease(s) granted.";

        return this.Page();
    }
}