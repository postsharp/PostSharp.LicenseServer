// Copyright (c) SharpCrafters s.r.o. See the LICENSE.md file in the root directory of this repository root for details.

using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using SharpCrafters.Backstage.LicenseServer.Data;

namespace SharpCrafters.Backstage.LicenseServer.Pages.Admin;

/// <summary>
/// Ends a lease before its end time, so that another user can take the seat.
/// </summary>
public sealed class CancelModel : PageModel
{
    private readonly ILeaseRepository repository;
    private readonly TimeProvider timeProvider;

    public CancelModel( ILeaseRepository repository, TimeProvider timeProvider )
    {
        this.repository = repository;
        this.timeProvider = timeProvider;
    }

    [BindProperty( SupportsGet = true )]
    public int Id { get; set; }

    public Lease? Lease { get; private set; }

    public async Task<IActionResult> OnGetAsync( CancellationToken cancellationToken )
    {
        this.Lease = await this.repository.OpenLeases
            .Include( l => l.License )
            .AsNoTracking()
            .SingleOrDefaultAsync( l => l.LeaseId == this.Id, cancellationToken );

        return this.Lease == null ? this.NotFound() : this.Page();
    }

    public async Task<IActionResult> OnPostAsync( CancellationToken cancellationToken )
    {
        var lease = await this.repository.OpenLeases
            .Include( l => l.License )
            .SingleOrDefaultAsync( l => l.LeaseId == this.Id, cancellationToken );

        if ( lease == null )
        {
            return this.NotFound();
        }

        this.repository.CancelLease(
            lease,
            this.User.Identity?.Name ?? string.Empty,
            this.timeProvider.GetUtcNow().UtcDateTime );

        await this.repository.SaveChangesAsync( cancellationToken );

        return this.RedirectToPage( "/Admin/Details", new { id = lease.LicenseId } );
    }
}