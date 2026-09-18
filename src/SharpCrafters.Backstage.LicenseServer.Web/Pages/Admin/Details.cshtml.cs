using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using SharpCrafters.Backstage.LicenseServer.Data;
using SharpCrafters.Backstage.LicenseServer.Options;

namespace SharpCrafters.Backstage.LicenseServer.Pages.Admin;

/// <summary>
/// The leases currently held against one license, and the actions an administrator can take on it.
/// </summary>
public sealed class DetailsModel(
    ILeaseRepository repository,
    LicenseServerDbContext db,
    IOptions<LicenseServerOptions> options,
    TimeProvider timeProvider ) : PageModel
{
    [BindProperty( SupportsGet = true )]
    public int Id { get; set; }

    public IReadOnlyList<Lease> Leases { get; private set; } = [];

    public int Seats { get; private set; }

    /// <summary>
    /// Gets the number of machines that one seat covers, so that the page states the rule with the
    /// value configured on this server and not with the default value.
    /// </summary>
    public int MachinesPerSeat => options.Value.MachinesPerUser;

    /// <summary>
    /// Gets <see cref="MachinesPerSeat"/> with its noun, so that a server configured with one
    /// machine per seat displays "1 machine" and not "1 machines".
    /// </summary>
    public string MachinesPerSeatText
        => this.MachinesPerSeat == 1 ? "1 machine" : $"{this.MachinesPerSeat} machines";

    public bool IsDisabled { get; private set; }

    public async Task<IActionResult> OnGetAsync( CancellationToken cancellationToken )
    {
        License? license = await repository.Licenses
            .AsNoTracking()
            .SingleOrDefaultAsync( l => l.LicenseId == this.Id, cancellationToken );

        if ( license == null )
        {
            return this.NotFound();
        }

        DateTime now = timeProvider.GetUtcNow().UtcDateTime;

        this.Leases = await repository.OpenLeases
            .Where( l => l.LicenseId == this.Id && l.StartTime <= now && l.EndTime >= now )
            .OrderBy( l => l.StartTime )
            .AsNoTracking()
            .ToListAsync( cancellationToken );

        this.Seats = repository.GetActiveSeats( this.Id, now );
        this.IsDisabled = license.Priority < 0;

        return this.Page();
    }

    public Task<IActionResult> OnPostEnableAsync( CancellationToken cancellationToken )
        => this.SetPriorityAsync( 0, cancellationToken );

    public Task<IActionResult> OnPostDisableAsync( CancellationToken cancellationToken )
        => this.SetPriorityAsync( -1, cancellationToken );

    private async Task<IActionResult> SetPriorityAsync( int priority, CancellationToken cancellationToken )
    {
        License? license = await db.Licenses.SingleOrDefaultAsync( l => l.LicenseId == this.Id, cancellationToken );

        if ( license == null )
        {
            return this.NotFound();
        }

        license.Priority = priority;
        await db.SaveChangesAsync( cancellationToken );

        return this.RedirectToPage( "/Index" );
    }

    public async Task<IActionResult> OnPostDeleteAsync( CancellationToken cancellationToken )
    {
        if ( !await db.Licenses.AnyAsync( l => l.LicenseId == this.Id, cancellationToken ) )
        {
            return this.NotFound();
        }

        await using var transaction = await db.Database.BeginTransactionAsync( cancellationToken );

        // The leases reference each other through the chain of replacements, so they are deleted
        // before the license, and the most recent ones before the ones they replaced.
        List<Lease> leases = await db.Leases
            .Where( l => l.LicenseId == this.Id )
            .OrderByDescending( l => l.LeaseId )
            .ToListAsync( cancellationToken );

        foreach ( Lease lease in leases )
        {
            db.Leases.Remove( lease );
            await db.SaveChangesAsync( cancellationToken );
        }

        await db.Licenses.Where( l => l.LicenseId == this.Id ).ExecuteDeleteAsync( cancellationToken );
        await transaction.CommitAsync( cancellationToken );

        return this.RedirectToPage( "/Index" );
    }
}
