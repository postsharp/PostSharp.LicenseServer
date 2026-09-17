using System.Net;
using System.Text.Json;
using PostSharp.LicenseServer.Tests.Infrastructure;

namespace PostSharp.LicenseServer.Tests;

/// <summary>
/// The pages an administrator uses, exercised through the real pipeline.
/// </summary>
public sealed class PageTests : IDisposable
{
    private readonly LicenseServerApplication application = new();

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
        Assert.Equal( 90, root.GetProperty( "used" ).GetArrayLength() );
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
