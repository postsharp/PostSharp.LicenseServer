using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace PostSharp.LicenseServer.Pages.Admin;

/// <summary>
/// Chooses the range of months to export from the lease audit log.
/// </summary>
public sealed class ExportModel( TimeProvider timeProvider ) : PageModel
{
    [BindProperty]
    [Range( 2010, 2100, ErrorMessage = "The year must be between 2010 and 2100." )]
    [Display( Name = "From year" )]
    public int FromYear { get; set; }

    [BindProperty]
    [Range( 1, 12 )]
    [Display( Name = "From month" )]
    public int FromMonth { get; set; }

    [BindProperty]
    [Range( 2010, 2100, ErrorMessage = "The year must be between 2010 and 2100." )]
    [Display( Name = "To year" )]
    public int ToYear { get; set; }

    [BindProperty]
    [Range( 1, 12 )]
    [Display( Name = "To month" )]
    public int ToMonth { get; set; }

    public static SelectList Months { get; } = new(
        Enumerable.Range( 1, 12 )
            .Select( m => new { Value = m, Text = System.Globalization.CultureInfo.CurrentCulture.DateTimeFormat.GetMonthName( m ) } ),
        "Value",
        "Text" );

    public void OnGet()
    {
        DateTime now = timeProvider.GetUtcNow().UtcDateTime;

        this.FromYear = now.Year;
        this.ToYear = now.Year;
        this.FromMonth = 1;
        this.ToMonth = now.Month;
    }

    public IActionResult OnPost()
    {
        if ( !this.ModelState.IsValid )
        {
            return this.Page();
        }

        if ( new DateTime( this.ToYear, this.ToMonth, 1 ) < new DateTime( this.FromYear, this.FromMonth, 1 ) )
        {
            this.ModelState.AddModelError( string.Empty, "The end of the range is before its start." );

            return this.Page();
        }

        return this.Redirect(
            $"/Admin/Export.ashx?fy={this.FromYear}&fm={this.FromMonth}&ty={this.ToYear}&tm={this.ToMonth}" );
    }
}
