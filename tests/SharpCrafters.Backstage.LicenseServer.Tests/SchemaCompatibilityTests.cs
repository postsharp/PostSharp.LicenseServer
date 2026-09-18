using Microsoft.EntityFrameworkCore;
using SharpCrafters.Backstage.LicenseServer.Data;

namespace SharpCrafters.Backstage.LicenseServer.Tests;

/// <summary>
/// The schema an existing installation already has, which this version must keep using unchanged.
/// </summary>
/// <remarks>
/// The project contains no EF migration. <c>CreateTables.sql</c> defines the schema, and the server
/// never creates and never modifies a SQL Server schema. These tests replace a migration: they
/// verify that the model uses the column types, the generation of the keys and the names of the
/// constraints that <c>CreateTables.sql</c> produces. Without them, a customer would discover a
/// mapping that changed, on their own data.
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
    /// In Transact-SQL, a <c>text</c> column cannot appear in a comparison, in an ORDER BY clause, in
    /// a GROUP BY clause, or in a DISTINCT clause, and the query fails at run time. A query may
    /// therefore only read the license key.
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
    /// The timestamps keep the type <c>datetime</c>. The default type of EF is <c>datetime2</c>, and
    /// SQL Server then converts the column at every comparison of the start time or of the end time
    /// of a lease.
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
    /// The deletion of a license must not cascade through the chain of replaced leases.
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
