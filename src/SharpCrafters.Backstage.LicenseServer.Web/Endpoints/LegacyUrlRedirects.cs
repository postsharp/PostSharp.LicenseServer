namespace SharpCrafters.Backstage.LicenseServer.Endpoints;

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

    /// <summary>
    /// Works out where a legacy URL should redirect to.
    /// </summary>
    /// <remarks>
    /// The path base matters: installed as an application below an IIS site root, a redirect to
    /// <c>/Graph</c> would land on the parent site rather than on this one. The query string carries
    /// the license identifier and the graph window, so it has to survive as well.
    /// </remarks>
    public static string BuildRedirectLocation( PathString pathBase, string target, QueryString queryString )
    {
        if ( !pathBase.HasValue )
        {
            return target + queryString;
        }

        // The home page is the path base itself, rather than the path base followed by a slash.
        string path = target == "/" ? pathBase.Value! : pathBase.Value + target;

        return path + queryString;
    }

    public static void MapLegacyUrlRedirects( this WebApplication app )
    {
        foreach ( (string legacy, string current) in redirects )
        {
            string target = current;

            app.MapGet(
                legacy,
                ( HttpContext context ) => Results.Redirect(
                    BuildRedirectLocation( context.Request.PathBase, target, context.Request.QueryString ),
                    true ) );
        }
    }
}
