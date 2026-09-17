using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using SharpCrafters.Backstage.LicenseServer.Data;

namespace SharpCrafters.Backstage.LicenseServer.Pages.Admin;

/// <summary>
/// Ends a lease early, freeing the seat for somebody else.
/// </summary>
public sealed class CancelModel( ILeaseRepository repository, TimeProvider timeProvider ) : PageModel
{
    [BindProperty( SupportsGet = true )]
    public int Id { get; set; }

    public Lease? Lease { get; private set; }

    public async Task<IActionResult> OnGetAsync( CancellationToken cancellationToken )
    {
        this.Lease = await repository.OpenLeases
            .Include( l => l.License )
            .AsNoTracking()
            .SingleOrDefaultAsync( l => l.LeaseId == this.Id, cancellationToken );

        return this.Lease == null ? this.NotFound() : this.Page();
    }

    public async Task<IActionResult> OnPostAsync( CancellationToken cancellationToken )
    {
        Lease? lease = await repository.OpenLeases
            .Include( l => l.License )
            .SingleOrDefaultAsync( l => l.LeaseId == this.Id, cancellationToken );

        if ( lease == null )
        {
            return this.NotFound();
        }

        repository.CancelLease(
            lease,
            this.User.Identity?.Name ?? string.Empty,
            timeProvider.GetUtcNow().UtcDateTime );

        await repository.SaveChangesAsync( cancellationToken );

        return this.RedirectToPage( "/Admin/Details", new { id = lease.LicenseId } );
    }
}
