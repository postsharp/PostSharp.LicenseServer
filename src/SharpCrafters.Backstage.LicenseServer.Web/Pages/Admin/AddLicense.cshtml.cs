using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using SharpCrafters.Backstage.LicenseServer.Data;
using SharpCrafters.Backstage.LicenseServer.Licensing;

namespace SharpCrafters.Backstage.LicenseServer.Pages.Admin;

/// <summary>
/// Registers a license key so that the server can serve leases against it.
/// </summary>
public sealed class AddLicenseModel(
    ILeaseRepository repository,
    LicenseServerDbContext db,
    ILicenseParser licenseParser,
    TimeProvider timeProvider ) : PageModel
{
    [BindProperty]
    [Required( ErrorMessage = "Paste the license key." )]
    [Display( Name = "License key" )]
    public string LicenseKey { get; set; } = "";

    public void OnGet() { }

    public async Task<IActionResult> OnPostAsync( CancellationToken cancellationToken )
    {
        if ( !this.ModelState.IsValid )
        {
            return this.Page();
        }

        LicenseInfo? parsedLicense = licenseParser.TryParse( this.LicenseKey );

        if ( parsedLicense == null )
        {
            this.ModelState.AddModelError( nameof(this.LicenseKey), "Invalid license key." );

            return this.Page();
        }

        if ( !parsedLicense.IsLicenseServerEligible )
        {
            this.ModelState.AddModelError(
                nameof(this.LicenseKey),
                $"Cannot add a {parsedLicense.LicenseTypeName ?? "(unknown license type)"} of "
                + $"{parsedLicense.ProductName} to the server." );

            return this.Page();
        }

        if ( await repository.Licenses.AnyAsync( l => l.LicenseId == parsedLicense.LicenseId, cancellationToken ) )
        {
            this.ModelState.AddModelError( nameof(this.LicenseKey), "The given license has been added already." );

            return this.Page();
        }

        db.Licenses.Add(
            new License
            {
                LicenseId = parsedLicense.LicenseId,
                LicenseKey = licenseParser.CleanLicenseString( this.LicenseKey ),
                CreatedOn = timeProvider.GetUtcNow().UtcDateTime,
                ProductCode = parsedLicense.Product
            } );

        await db.SaveChangesAsync( cancellationToken );

        return this.RedirectToPage( "/Index" );
    }
}
