using System.Diagnostics;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace PostSharp.LicenseServer.Pages;

[ResponseCache( Duration = 0, Location = ResponseCacheLocation.None, NoStore = true )]
public sealed class ErrorModel : PageModel
{
    /// <summary>
    /// Gets the identifier an administrator can use to find this request in the log.
    /// </summary>
    public string? RequestId { get; private set; }

    public void OnGet() => this.RequestId = Activity.Current?.Id ?? this.HttpContext.TraceIdentifier;
}
