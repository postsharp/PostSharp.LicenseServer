// Copyright (c) SharpCrafters s.r.o. See the LICENSE.md file in the root directory of this repository root for details.

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
public sealed class AddLicenseModel : PageModel
{
    private readonly ILeaseRepository repository;
    private readonly LicenseServerDbContext db;
    private readonly ILicenseParser licenseParser;
    private readonly TimeProvider timeProvider;

    public AddLicenseModel(
        ILeaseRepository repository,
        LicenseServerDbContext db,
        ILicenseParser licenseParser,
        TimeProvider timeProvider )
    {
        this.repository = repository;
        this.db = db;
        this.licenseParser = licenseParser;
        this.timeProvider = timeProvider;
    }

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

        var parsedLicense = this.licenseParser.TryParse( this.LicenseKey );

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

        if ( await this.repository.Licenses.AnyAsync( l => l.LicenseId == parsedLicense.LicenseId, cancellationToken ) )
        {
            this.ModelState.AddModelError( nameof(this.LicenseKey), "The given license has been added already." );

            return this.Page();
        }

        this.db.Licenses.Add(
            new License
            {
                LicenseId = parsedLicense.LicenseId,
                LicenseKey = this.licenseParser.CleanLicenseString( this.LicenseKey ),
                CreatedOn = this.timeProvider.GetUtcNow().UtcDateTime,
                ProductCode = parsedLicense.Product
            } );

        await this.db.SaveChangesAsync( cancellationToken );

        return this.RedirectToPage( "/Index" );
    }
}