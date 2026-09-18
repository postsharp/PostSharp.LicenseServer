using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging.Abstractions;
using SharpCrafters.Backstage.LicenseServer.Data;
using SharpCrafters.Backstage.LicenseServer.Licensing;
using SharpCrafters.Backstage.LicenseServer.Tests.Infrastructure;

// Aliased because SharpCrafters.Backstage.LicenseServer.Licensing, the namespace of this product,
// shadows SharpCrafters.Backstage.Licensing, the namespace of the licensing component.
using LicenseProduct = SharpCrafters.Backstage.Licensing.LicenseProduct;
using LicenseType = SharpCrafters.Backstage.Licensing.LicenseType;

namespace SharpCrafters.Backstage.LicenseServer.Tests;

/// <summary>
/// The license keys a development server issues to itself, so that a trial or a load simulation has
/// something to lease without anybody buying a license first.
/// </summary>
public sealed class SeedTestLicensesTests : IDisposable
{
    private readonly string dataDirectory =
        Path.Combine( Path.GetTempPath(), "licenseserver-tests", Guid.NewGuid().ToString( "N" ) );

    public void Dispose()
    {
        if ( Directory.Exists( this.dataDirectory ) )
        {
            Directory.Delete( this.dataDirectory, true );
        }
    }

    private string KeyFile => Path.Combine( this.dataDirectory, "test-authority.key" );

    private TestLicenseAuthority CreateAuthority()
        => TestLicenseAuthority.LoadOrCreate( this.KeyFile, NullLogger.Instance );

    /// <summary>
    /// The key pair has to outlive the process, because the license keys it signed are in the
    /// database and stop verifying when the pair changes. In a container that is what makes the data
    /// directory a volume rather than part of the container.
    /// </summary>
    [Fact]
    public void Authority_IsReusedAcrossProcesses()
    {
        string licenseKey = this.CreateAuthority()
            .CreateLicenseKey( 900001, LicenseProduct.MetalamaProfessional, LicenseType.Business, 25, 5, 20, DateTime.UtcNow.AddYears( 1 ) );

        Assert.True( File.Exists( this.KeyFile ) );

        // A second instance reads the file rather than generating a new pair, so a key signed before
        // a restart is still accepted after it.
        BackstageLicenseParser parser = new( this.CreateAuthority().Authority );

        Assert.NotNull( parser.TryParse( licenseKey ) );
    }

    [Fact]
    public void Authority_OfAnotherServer_IsNotAccepted()
    {
        string licenseKey = this.CreateAuthority()
            .CreateLicenseKey( 900001, LicenseProduct.MetalamaProfessional, LicenseType.Business, 25, 5, 20, DateTime.UtcNow.AddYears( 1 ) );

        string otherDirectory = Path.Combine( this.dataDirectory, "other" );

        TestLicenseAuthority other =
            TestLicenseAuthority.LoadOrCreate( Path.Combine( otherDirectory, "test-authority.key" ), NullLogger.Instance );

        Assert.Null( new BackstageLicenseParser( other.Authority ).TryParse( licenseKey ) );
    }

    [Fact]
    public async Task Seed_EmptyDatabase_AddsLicensesThisServerAccepts()
    {
        await using LicenseServerTestContext context = await LicenseServerTestContext.CreateAsync();
        TestLicenseAuthority authority = this.CreateAuthority();

        IReadOnlyList<int> added = TestLicenseSeeder.Seed(
            context.Db,
            authority,
            TimeProvider.System,
            NullLogger.Instance );

        Assert.Equal( 2, added.Count );

        BackstageLicenseParser parser = new( authority.Authority );

        foreach ( License license in context.Db.Licenses )
        {
            LicenseInfo? parsed = parser.TryParse( license.LicenseKey );

            Assert.NotNull( parsed );
            Assert.True( parsed.IsLicenseServerEligible );
            Assert.Equal( license.ProductCode, parsed.Product );
        }
    }

    /// <summary>
    /// Seeding twice leaves the first set alone, so a server restarted against the same database
    /// keeps the leases it has granted.
    /// </summary>
    [Fact]
    public async Task Seed_Twice_AddsNothingTheSecondTime()
    {
        await using LicenseServerTestContext context = await LicenseServerTestContext.CreateAsync();
        TestLicenseAuthority authority = this.CreateAuthority();

        TestLicenseSeeder.Seed( context.Db, authority, TimeProvider.System, NullLogger.Instance );

        // The keys are sorted after they are read. On SQL Server the column has the type text, which
        // Transact-SQL refuses to sort, and the query would fail. This is the rule that
        // SchemaCompatibilityTests states for the queries of the server.
        string[] first = ReadKeys( context );

        Assert.Empty( TestLicenseSeeder.Seed( context.Db, authority, TimeProvider.System, NullLogger.Instance ) );
        Assert.Equal( first, ReadKeys( context ) );

        static string[] ReadKeys( LicenseServerTestContext context )
            => context.Db.Licenses.Select( l => l.LicenseKey ).AsEnumerable().Order().ToArray();
    }

    /// <summary>
    /// Outside the Development environment, the server refuses to start instead of serving license
    /// keys that it issued to itself.
    /// </summary>
    [Theory]
    [InlineData( "Production" )]
    [InlineData( "Staging" )]
    public void SeedTestLicenses_OutsideDevelopment_RefusesToStart( string environmentName )
    {
        IConfiguration configuration = new ConfigurationBuilder()
            .AddInMemoryCollection( new Dictionary<string, string?> { ["LicenseServer:SeedTestLicenses"] = "true" } )
            .Build();

        ServiceCollection services = [];

        InvalidOperationException exception = Assert.Throws<InvalidOperationException>(
            () => services.AddLicenseServerLicensing( configuration, new StubEnvironment( environmentName, this.dataDirectory ) ) );

        Assert.Contains( "SeedTestLicenses", exception.Message, StringComparison.Ordinal );
        Assert.Contains( environmentName, exception.Message, StringComparison.Ordinal );
    }

    /// <summary>
    /// A server that asks for none of this starts with the production authority alone, which is what
    /// the container image does by default.
    /// </summary>
    [Fact]
    public void NoTestSettings_OutsideDevelopment_Starts()
    {
        IConfiguration configuration = new ConfigurationBuilder().AddInMemoryCollection( [] ).Build();

        ServiceCollection services = [];

        Assert.Empty( services.AddLicenseServerLicensing( configuration, new StubEnvironment( "Production", this.dataDirectory ) ) );
        Assert.Null( services.BuildServiceProvider().GetService<TestLicenseAuthority>() );
    }

    private sealed class StubEnvironment( string environmentName, string contentRootPath = "." ) : IHostEnvironment
    {
        public string EnvironmentName { get; set; } = environmentName;

        public string ApplicationName { get; set; } = "Tests";

        public string ContentRootPath { get; set; } = contentRootPath;

        public Microsoft.Extensions.FileProviders.IFileProvider ContentRootFileProvider { get; set; } =
            new Microsoft.Extensions.FileProviders.NullFileProvider();
    }
}
