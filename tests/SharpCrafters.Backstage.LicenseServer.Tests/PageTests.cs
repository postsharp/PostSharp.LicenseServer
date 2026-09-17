using System.Net;
using System.Text.Json;
using System.Text.RegularExpressions;
using SharpCrafters.Backstage.LicenseServer.Data;
using SharpCrafters.Backstage.LicenseServer.Tests.Infrastructure;

namespace SharpCrafters.Backstage.LicenseServer.Tests;

/// <summary>
/// The pages an administrator uses, exercised through the real pipeline.
/// </summary>
public sealed partial class PageTests : IDisposable
{
    private readonly LicenseServerApplication application = new();

    /// <summary>
    /// Collapses every run of whitespace, so that an assertion on a sentence does not depend on where
    /// the markup wraps it.
    /// </summary>
    [GeneratedRegex( @"\s+" )]
    private static partial Regex Whitespace();

    /// <summary>
    /// Captures the action of a form and its contents, so that a test can submit it the way a browser
    /// would.
    /// </summary>
    [GeneratedRegex( "<form[^>]*action=\"([^\"]+)\"[^>]*>(.*?)</form>", RegexOptions.Singleline )]
    private static partial Regex Form();

    [GeneratedRegex( "name=\"__RequestVerificationToken\"[^>]*value=\"([^\"]+)\"" )]
    private static partial Regex AntiforgeryToken();

    public void Dispose() => this.application.Dispose();

    [Theory]
    [InlineData( "/" )]
    [InlineData( "/Admin/AddLicense" )]
    [InlineData( "/Admin/Export" )]
    public async Task Page_IsServed( string url )
    {
        HttpClient client = this.application.CreateClient();

        HttpResponseMessage response = await client.GetAsync( url );

        Assert.Equal( HttpStatusCode.OK, response.StatusCode );
    }

    [Fact]
    public async Task Index_NoLicenses_InvitesTheAdministratorToAddOne()
    {
        HttpClient client = this.application.CreateClient();

        string body = await client.GetStringAsync( "/" );

        Assert.Contains( "No license has been registered yet", body, StringComparison.Ordinal );
    }

    [Fact]
    public async Task Index_ListsTheLicense()
    {
        this.application.AddLicense( LicenseBuilder.Default().WithLicenseId( 42 ).WithUsers( 7 ) );
        HttpClient client = this.application.CreateClient();

        string body = await client.GetStringAsync( "/" );

        Assert.Contains( "42", body, StringComparison.Ordinal );
        Assert.Contains( "Ultimate", body, StringComparison.Ordinal );
    }

    [Fact]
    public async Task Details_IsServed()
    {
        this.application.AddLicense( LicenseBuilder.Default().WithLicenseId( 3 ) );
        HttpClient client = this.application.CreateClient();

        HttpResponseMessage response = await client.GetAsync( "/Admin/Details?id=3" );

        Assert.Equal( HttpStatusCode.OK, response.StatusCode );
    }

    /// <summary>
    /// The page states what a seat is, because it reports a number of them. The rule is stated with
    /// the value this server is configured with, so an installation that changed it is not told the
    /// default.
    /// </summary>
    [Fact]
    public async Task Details_ExplainsWhatASeatIs()
    {
        this.application.AddLicense( LicenseBuilder.Default().WithLicenseId( 3 ) );
        HttpClient client = this.application.CreateClient();

        // The markup wraps the sentence over several lines, and where it wraps is not what this test
        // is about.
        string body = Whitespace().Replace( await client.GetStringAsync( "/Admin/Details?id=3" ), " " );

        // LicenseServerApplication configures two machines per seat.
        Assert.Contains( "A seat is one user working on up to 2 machines.", body, StringComparison.Ordinal );

        Assert.Contains(
            "the number of machines divided by 2, rounded up",
            body,
            StringComparison.Ordinal );
    }
    /// <summary>
    /// The actions that change a license sit in one menu at the top of the page, and each of them
    /// carries the text of the confirmation it asks for before it runs.
    /// </summary>
    [Fact]
    public async Task Details_OffersItsActionsInAMenu()
    {
        this.application.AddLicense( LicenseBuilder.Default().WithLicenseId( 3 ) );
        HttpClient client = this.application.CreateClient();

        string body = await client.GetStringAsync( "/Admin/Details?id=3" );

        Assert.Contains( ">Manage</summary>", body, StringComparison.Ordinal );
        Assert.Contains( "handler=Disable", body, StringComparison.Ordinal );
        Assert.Contains( "data-confirm=\"Disable license 3?\"", body, StringComparison.Ordinal );

        // An enabled license is not offered for deletion: it is disabled first.
        Assert.DoesNotContain( "handler=Delete", body, StringComparison.Ordinal );
    }

    /// <summary>
    /// A disabled license says so on the page itself, because the menu that holds the state is closed
    /// until the administrator opens it.
    /// </summary>
    [Fact]
    public async Task Details_DisabledLicense_SaysSoAndOffersToEnableOrDeleteIt()
    {
        this.application.AddLicense( LicenseBuilder.Default().WithLicenseId( 3 ).WithPriority( -1 ) );
        HttpClient client = this.application.CreateClient();

        string body = Whitespace().Replace( await client.GetStringAsync( "/Admin/Details?id=3" ), " " );

        Assert.Contains( "This license is disabled: it serves no new lease.", body, StringComparison.Ordinal );
        Assert.Contains( "handler=Enable", body, StringComparison.Ordinal );
        Assert.Contains( "handler=Delete", body, StringComparison.Ordinal );
    }
    /// <summary>
    /// The action posts against the license the page is about. The license is named in the query
    /// string, which a form does not inherit from the page that contains it, so an action that does
    /// not name it again reaches no license at all.
    /// </summary>
    [Fact]
    public async Task Details_Disable_DisablesThatLicense()
    {
        this.application.AddLicense( LicenseBuilder.Default().WithLicenseId( 3 ) );

        HttpResponseMessage response = await this.SubmitDetailsActionAsync( 3, "handler=Disable" );

        Assert.Equal( HttpStatusCode.Found, response.StatusCode );

        using LicenseServerDbContext db = this.application.CreateDbContext();

        Assert.True( db.Licenses.Single( l => l.LicenseId == 3 ).Priority < 0 );
    }

    [Fact]
    public async Task Details_Delete_RemovesThatLicense()
    {
        this.application.AddLicense( LicenseBuilder.Default().WithLicenseId( 3 ).WithPriority( -1 ) );
        this.application.AddLicense( LicenseBuilder.Default().WithLicenseId( 4 ) );

        HttpResponseMessage response = await this.SubmitDetailsActionAsync( 3, "handler=Delete" );

        Assert.Equal( HttpStatusCode.Found, response.StatusCode );

        using LicenseServerDbContext db = this.application.CreateDbContext();

        Assert.Equal( [4], db.Licenses.Select( l => l.LicenseId ).ToArray() );
    }



    [Fact]
    public async Task Details_UnknownLicense_Returns404()
    {
        HttpClient client = this.application.CreateClient();

        Assert.Equal( HttpStatusCode.NotFound, ( await client.GetAsync( "/Admin/Details?id=999" ) ).StatusCode );
    }

    [Fact]
    public async Task Graph_IsServedWithItsDataEmbedded()
    {
        this.application.AddLicense( LicenseBuilder.Default().WithLicenseId( 5 ).WithUsers( 10 ).WithGracePercent( 20 ) );
        HttpClient client = this.application.CreateClient();

        string body = await client.GetStringAsync( "/Graph?id=5&days=30" );

        Assert.Contains( "usage-chart-data", body, StringComparison.Ordinal );

        // Served from the application, not from a content delivery network.
        Assert.Contains( "/lib/chartjs/chart.umd.min.js", body, StringComparison.Ordinal );
        Assert.DoesNotContain( "yui.yahooapis.com", body, StringComparison.Ordinal );
        Assert.DoesNotContain( "http://", body, StringComparison.Ordinal );
    }

    [Fact]
    public async Task Graph_EmbeddedDataIsValidJsonCoveringTheWindow()
    {
        this.application.AddLicense( LicenseBuilder.Default().WithLicenseId( 5 ).WithUsers( 10 ).WithGracePercent( 20 ) );
        HttpClient client = this.application.CreateClient();

        string body = await client.GetStringAsync( "/Graph?id=5&days=90" );
        string json = ExtractChartData( body );

        using JsonDocument document = JsonDocument.Parse( json );
        JsonElement root = document.RootElement;

        Assert.Equal( 90, root.GetProperty( "labels" ).GetArrayLength() );
        Assert.Equal( 90, root.GetProperty( "seats" ).GetArrayLength() );
        Assert.Equal( 10, root.GetProperty( "maximum" ).GetInt32() );
        Assert.Equal( 12, root.GetProperty( "grace" ).GetInt32() );
        Assert.True( root.GetProperty( "axisMaximum" ).GetInt32() >= 12 );
    }

    [Fact]
    public async Task Graph_UnlimitedLicense_OmitsTheCapacityLines()
    {
        this.application.AddLicense( LicenseBuilder.Default().WithLicenseId( 6 ).WithUsers( null ) );
        HttpClient client = this.application.CreateClient();

        string json = ExtractChartData( await client.GetStringAsync( "/Graph?id=6" ) );

        using JsonDocument document = JsonDocument.Parse( json );

        Assert.Equal( JsonValueKind.Null, document.RootElement.GetProperty( "maximum" ).ValueKind );
        Assert.Equal( JsonValueKind.Null, document.RootElement.GetProperty( "grace" ).ValueKind );
    }

    [Fact]
    public async Task Graph_UnknownLicense_Returns404()
    {
        HttpClient client = this.application.CreateClient();

        Assert.Equal( HttpStatusCode.NotFound, ( await client.GetAsync( "/Graph?id=999" ) ).StatusCode );
    }

    /// <summary>
    /// The legacy page answered 500 for anything it could not parse.
    /// </summary>
    [Theory]
    [InlineData( "/Graph?id=5&days=7" )]
    [InlineData( "/Graph?id=5&days=99999" )]
    public async Task Graph_UnsupportedWindow_Returns400( string url )
    {
        this.application.AddLicense( LicenseBuilder.Default().WithLicenseId( 5 ) );
        HttpClient client = this.application.CreateClient();

        Assert.Equal( HttpStatusCode.BadRequest, ( await client.GetAsync( url ) ).StatusCode );
    }

    [Fact]
    public async Task GetTime_ReportsTheTimeAndTheAcceleration()
    {
        HttpClient client = this.application.CreateClient();

        string body = await client.GetStringAsync( "/GetTime.ashx" );
        string[] fields = body.Split( ';' );

        // The simulator parses this positionally.
        Assert.Equal( 2, fields.Length );
        Assert.EndsWith( "Z", fields[0], StringComparison.Ordinal );
        Assert.Equal( 1m, decimal.Parse( fields[1], System.Globalization.CultureInfo.InvariantCulture ) );
    }

    [Theory]
    [InlineData( "/Default.aspx", "/" )]
    [InlineData( "/Graph.aspx?id=5", "/Graph?id=5" )]
    [InlineData( "/Admin/Details.aspx?id=5", "/Admin/Details?id=5" )]
    [InlineData( "/Admin/AddLicense.aspx", "/Admin/AddLicense" )]
    public async Task LegacyUrl_RedirectsPermanently( string legacy, string expected )
    {
        HttpClient client = this.application.CreateClient(
            new Microsoft.AspNetCore.Mvc.Testing.WebApplicationFactoryClientOptions { AllowAutoRedirect = false } );

        HttpResponseMessage response = await client.GetAsync( legacy );

        Assert.Equal( HttpStatusCode.MovedPermanently, response.StatusCode );
        Assert.Equal( expected, response.Headers.Location?.OriginalString );
    }

    [Fact]
    public async Task DemoDataGenerator_IsNotAvailableOutsideDevelopment()
    {
        HttpClient client = this.application.CreateClient();

        Assert.Equal( HttpStatusCode.NotFound, ( await client.GetAsync( "/Admin/GenerateDemoData" ) ).StatusCode );
    }

    /// <summary>
    /// Submits one of the management forms of the details page as a browser would, with the action and
    /// the antiforgery token the page itself supplies.
    /// </summary>
    private async Task<HttpResponseMessage> SubmitDetailsActionAsync( int licenseId, string handler )
    {
        HttpClient client = this.application.CreateClient(
            new Microsoft.AspNetCore.Mvc.Testing.WebApplicationFactoryClientOptions { AllowAutoRedirect = false } );

        string page = await client.GetStringAsync( $"/Admin/Details?id={licenseId}" );

        Match form = Form().Matches( page ).SingleOrDefault( m => m.Groups[1].Value.Contains( handler, StringComparison.Ordinal ) )
                     ?? throw new InvalidOperationException( $"The page has no form posting to {handler}." );

        Match token = AntiforgeryToken().Match( form.Groups[2].Value );
        Assert.True( token.Success, "The form carries no antiforgery token." );

        return await client.PostAsync(
            WebUtility.HtmlDecode( form.Groups[1].Value ),
            new FormUrlEncodedContent(
                new Dictionary<string, string> { ["__RequestVerificationToken"] = token.Groups[1].Value } ) );
    }

    private static string ExtractChartData( string html )
    {
        const string opening = "<script id=\"usage-chart-data\" type=\"application/json\">";

        int start = html.IndexOf( opening, StringComparison.Ordinal );
        Assert.True( start >= 0, "The chart data element is missing from the page." );

        start += opening.Length;
        int end = html.IndexOf( "</script>", start, StringComparison.Ordinal );

        return System.Net.WebUtility.HtmlDecode( html[start..end] );
    }
}
