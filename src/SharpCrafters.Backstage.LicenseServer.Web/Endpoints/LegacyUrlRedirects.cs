// Copyright (c) SharpCrafters s.r.o. See the LICENSE.md file in the root directory of this repository root for details.

namespace SharpCrafters.Backstage.LicenseServer.Endpoints;

/// <summary>
/// Redirects the WebForms URLs of the previous versions to the pages that replaced them, so that
/// bookmarks and the links of old notification e-mails continue to work.
/// </summary>
public static class LegacyUrlRedirects
{
    private static readonly (string Legacy, string Current)[] redirects =
    [
        ( "/Default.aspx", "/" ),
        ( "/Graph.aspx", "/Graph" ),
        ( "/Admin/AddLicense.aspx", "/Admin/AddLicense" ),
        ( "/Admin/Cancel.aspx", "/Admin/Cancel" ),
        ( "/Admin/Details.aspx", "/Admin/Details" ),
        ( "/Admin/Export.aspx", "/Admin/Export" ),
        ( "/Admin/GenerateDemoData.aspx", "/Admin/GenerateDemoData" )
    ];

    /// <summary>
    /// Returns the target of the redirection of a legacy URL.
    /// </summary>
    /// <remarks>
    /// The target contains the path base. When the server is installed as an application below the
    /// root of an IIS site, a redirection to <c>/Graph</c> would reach the parent site and not this
    /// application. The target also contains the query string, which carries the identifier of the
    /// license and the window of the graph.
    /// </remarks>
    public static string BuildRedirectLocation( PathString pathBase, string target, QueryString queryString )
    {
        if ( !pathBase.HasValue )
        {
            return target + queryString;
        }

        // The home page is the path base itself, and not the path base followed by a slash.
        var path = target == "/" ? pathBase.Value! : pathBase.Value + target;

        return path + queryString;
    }

    public static void MapLegacyUrlRedirects( this WebApplication app )
    {
        foreach ( var (legacy, current) in redirects )
        {
            var target = current;

            app.MapGet(
                legacy,
                ( HttpContext context ) => Results.Redirect(
                    BuildRedirectLocation( context.Request.PathBase, target, context.Request.QueryString ),
                    true ) );
        }
    }
}