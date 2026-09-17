namespace PostSharp.LicenseServer.Endpoints;

/// <summary>
/// Redirects the WebForms URLs of previous versions to the pages that replaced them, so that
/// bookmarks and links in old notification emails keep working.
/// </summary>
public static class LegacyUrlRedirects
{
    private static readonly (string Legacy, string Current)[] redirects =
    [
        ("/Default.aspx", "/"),
        ("/Graph.aspx", "/Graph"),
        ("/Admin/AddLicense.aspx", "/Admin/AddLicense"),
        ("/Admin/Cancel.aspx", "/Admin/Cancel"),
        ("/Admin/Details.aspx", "/Admin/Details"),
        ("/Admin/Export.aspx", "/Admin/Export"),
        ("/Admin/GenerateDemoData.aspx", "/Admin/GenerateDemoData")
    ];

    public static void MapLegacyUrlRedirects( this WebApplication app )
    {
        foreach ( (string legacy, string current) in redirects )
        {
            string target = current;

            // The query string carries the license identifier and the graph window, so it has to
            // survive the redirect.
            app.MapGet( legacy, ( HttpContext context ) => Results.Redirect( target + context.Request.QueryString, true ) );
        }
    }
}
