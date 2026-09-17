using Microsoft.EntityFrameworkCore;
using SharpCrafters.Backstage.LicenseServer.Data;

namespace SharpCrafters.Backstage.LicenseServer.Tests;

/// <summary>
/// The schema an existing installation already has, which this version must keep using unchanged.
/// </summary>
/// <remarks>
/// There are deliberately no EF migrations: <c>CreateTables.sql</c> is the source of truth, and the
/// server never creates or alters a SQL Server schema. These tests are what stands in for a
/// migration, by holding the model to the column types, key generation and constraint names that
/// <c>CreateTables.sql</c> produces. A mapping that drifted would otherwise be discovered by a
/// customer, on their data.
/// </remarks>
public sealed class SchemaCompatibilityTests
{
    /// <summary>
    /// Generates the SQL Server DDL for the model. No server is contacted.
    /// </summary>
    private static string CreateScript()
    {
        DbContextOptions<LicenseServerDbContext> options =
            new DbContextOptionsBuilder<LicenseServerDbContext>()
                .UseSqlServer( "Server=none;Database=none;" )
                .Options;

        using LicenseServerDbContext db = new( options );

        return db.Database.GenerateCreateScript();
    }

    [Fact]
    public void Licenses_LicenseKeyIsStillText()
    {
        // CreateTables.sql declares [LicenseKey] [text]. Letting EF default to nvarchar(max) would
        // generate a schema that does not match an existing database.
        Assert.Contains( "[LicenseKey] text NOT NULL", CreateScript(), StringComparison.OrdinalIgnoreCase );
    }

    /// <summary>
    /// A <c>text</c> column cannot appear in a comparison, an ORDER BY, a GROUP BY or a DISTINCT in
    /// T-SQL, and the failure happens at run time. No query may therefore touch the license key
    /// other than to project it.
    /// </summary>
    [Fact]
    public void Licenses_LicenseKeyIsNeverFilteredOrSorted()
    {
        DbContextOptions<LicenseServerDbContext> options =
            new DbContextOptionsBuilder<LicenseServerDbContext>()
                .UseSqlServer( "Server=none;Database=none;" )
                .Options;

        using LicenseServerDbContext db = new( options );

        string sql = db.Licenses.OrderBy( l => l.Priority ).ThenByDescending( l => l.LicenseId ).ToQueryString();

        Assert.Contains( "[LicenseKey]", sql, StringComparison.OrdinalIgnoreCase );
        Assert.DoesNotContain( "ORDER BY [l].[LicenseKey]", sql, StringComparison.OrdinalIgnoreCase );
    }

    [Fact]
    public void Licenses_LicenseIdIsAssignedByTheApplication()
    {
        // The identifier comes from the license key. Making it an identity column would both break
        // an existing database and lose the link between the row and the key.
        string script = CreateScript();
        int licensesTable = script.IndexOf( "CREATE TABLE [Licenses]", StringComparison.OrdinalIgnoreCase );

        Assert.True( licensesTable >= 0 );

        string licenses = script[licensesTable..script.IndexOf( ");", licensesTable, StringComparison.Ordinal )];

        Assert.Contains( "[LicenseId] int NOT NULL", licenses, StringComparison.OrdinalIgnoreCase );
        Assert.DoesNotContain( "IDENTITY", licenses, StringComparison.OrdinalIgnoreCase );
    }

    [Fact]
    public void Leases_LeaseIdIsGeneratedByTheDatabase()
    {
        Assert.Contains( "[LeaseId] int NOT NULL IDENTITY", CreateScript(), StringComparison.OrdinalIgnoreCase );
    }

    [Theory]
    // Types as declared by CreateTables.sql.
    [InlineData( "[ProductCode] varchar(50) NOT NULL" )]
    [InlineData( "[Priority] int NOT NULL" )]
    [InlineData( "[CreatedOn] datetime NOT NULL" )]
    [InlineData( "[GraceStartTime] datetime NULL" )]
    [InlineData( "[GraceLastWarningTime] datetime NULL" )]
    [InlineData( "[StartTime] datetime NOT NULL" )]
    [InlineData( "[EndTime] datetime NOT NULL" )]
    [InlineData( "[UserName] nvarchar(200) NOT NULL" )]
    [InlineData( "[Machine] nvarchar(200) NOT NULL" )]
    [InlineData( "[AuthenticatedUser] nvarchar(200) NOT NULL" )]
    [InlineData( "[HMAC] varchar(100) NULL" )]
    [InlineData( "[Grace] bit NOT NULL" )]
    [InlineData( "[OverwrittenLeaseId] int NULL" )]
    [InlineData( "[LicenseId] int NOT NULL" )]
    public void Column_KeepsItsType( string expected )
        => Assert.Contains( expected, CreateScript(), StringComparison.OrdinalIgnoreCase );

    /// <summary>
    /// Timestamps must stay <c>datetime</c>. EF would default them to <c>datetime2</c>, which makes
    /// SQL Server convert the column on every comparison of a lease's start or end time.
    /// </summary>
    [Fact]
    public void Timestamps_AreNeverDateTime2()
        => Assert.DoesNotContain( "datetime2", CreateScript(), StringComparison.OrdinalIgnoreCase );

    [Theory]
    [InlineData( "PK_Licenses" )]
    [InlineData( "PK_Leases" )]
    [InlineData( "FK_Leases_Licenses" )]
    [InlineData( "FK_Leases_Leases" )]
    [InlineData( "IX_Leases_EndTime" )]
    [InlineData( "IX_Leases_OverwrittenLeaseId" )]
    public void Constraint_KeepsItsName( string expected )
        => Assert.Contains( expected, CreateScript(), StringComparison.Ordinal );

    [Fact]
    public void Tables_KeepTheirNames()
    {
        string script = CreateScript();

        Assert.Contains( "CREATE TABLE [Licenses]", script, StringComparison.OrdinalIgnoreCase );
        Assert.Contains( "CREATE TABLE [Leases]", script, StringComparison.OrdinalIgnoreCase );
    }

    /// <summary>
    /// Deleting a license must not silently cascade through the chain of replaced leases.
    /// </summary>
    [Fact]
    public void ForeignKeys_DoNotCascade()
        => Assert.DoesNotContain( "ON DELETE CASCADE", CreateScript(), StringComparison.OrdinalIgnoreCase );

    [Fact]
    public void Model_HasExactlyTheTwoExpectedTables()
    {
        DbContextOptions<LicenseServerDbContext> options =
            new DbContextOptionsBuilder<LicenseServerDbContext>()
                .UseSqlServer( "Server=none;Database=none;" )
                .Options;

        using LicenseServerDbContext db = new( options );

        string[] tables = db.Model.GetEntityTypes()
            .Select( e => e.GetTableName() )
            .Where( name => name != null )
            .Distinct()
            .Order()
            .ToArray()!;

        Assert.Equal( ["Leases", "Licenses"], tables );
    }
}
