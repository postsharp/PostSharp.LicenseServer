using SharpCrafters.Backstage.LicenseServer.Licensing;

namespace SharpCrafters.Backstage.LicenseServer.Tests.Infrastructure;

/// <summary>
/// Fixed points in time used by the tests.
/// </summary>
public static class TestClock
{
    /// <summary>
    /// A Monday, at a whole second.
    /// </summary>
    /// <remarks>
    /// Monday exercises the branch of the usage graph that labels the weekdays. Whole seconds keep
    /// the tests independent of the rounding of the SQL type <c>datetime</c>, which is 1/300 of a
    /// second.
    /// </remarks>
    public static readonly DateTime Origin = new( 2026, 1, 5, 9, 0, 0, DateTimeKind.Utc );

    public static DateTime Days( double days ) => Origin.AddDays( days );

    public static DateTime Hours( double hours ) => Origin.AddHours( hours );
}

/// <summary>
/// Builds a license, and the properties that the fake parser reports for its key.
/// </summary>
public sealed class LicenseBuilder
{
    private int licenseId = 1;
    private int? userNumber = 5;
    private int priority;
    private string product = "Ultimate";
    private string licenseType = "PerUser";
    private DateTime? validTo;
    private DateTime? subscriptionEndDate;
    private Version minPostSharpVersion = new( 1, 0, 0 );
    private int graceDays = 30;
    private int gracePercent = 20;
    private bool isLicenseServerEligible = true;
    private DateTime? graceStartTime;

    public static LicenseBuilder Default() => new();

    public LicenseBuilder WithLicenseId( int value )
    {
        this.licenseId = value;

        return this;
    }

    /// <summary>
    /// Sets the number of concurrent users, or null for an unlimited license.
    /// </summary>
    public LicenseBuilder WithUsers( int? value )
    {
        this.userNumber = value;

        return this;
    }

    /// <summary>
    /// Gets the priority of the license, which is negative when the license is disabled.
    /// </summary>
    public int Priority => this.priority;

    public LicenseBuilder WithPriority( int value )
    {
        this.priority = value;

        return this;
    }

    public LicenseBuilder WithProduct( string value )
    {
        this.product = value;

        return this;
    }

    public LicenseBuilder WithLicenseType( string value )
    {
        this.licenseType = value;

        return this;
    }

    public LicenseBuilder WithValidTo( DateTime? value )
    {
        this.validTo = value;

        return this;
    }

    public LicenseBuilder WithSubscriptionEndDate( DateTime? value )
    {
        this.subscriptionEndDate = value;

        return this;
    }

    public LicenseBuilder WithMinPostSharpVersion( Version value )
    {
        this.minPostSharpVersion = value;

        return this;
    }

    public LicenseBuilder WithGraceDays( int value )
    {
        this.graceDays = value;

        return this;
    }

    public LicenseBuilder WithGracePercent( int value )
    {
        this.gracePercent = value;

        return this;
    }

    public LicenseBuilder NotLicenseServerEligible()
    {
        this.isLicenseServerEligible = false;

        return this;
    }

    public LicenseBuilder WithGraceStartTime( DateTime? value )
    {
        this.graceStartTime = value;

        return this;
    }

    public LicenseInfo BuildInfo()
        => new()
        {
            LicenseId = this.licenseId,
            Product = this.product,
            LicenseType = this.licenseType,
            UserNumber = this.userNumber,
            ValidTo = this.validTo,
            SubscriptionEndDate = this.subscriptionEndDate,
            MinPostSharpVersion = this.minPostSharpVersion,
            GraceDays = this.graceDays,
            GracePercent = this.gracePercent,
            IsLicenseServerEligible = this.isLicenseServerEligible,
            LicenseTypeName = this.licenseType,
            ProductName = this.product
        };

    /// <summary>
    /// Adds the license to the database and registers its key with the fake parser.
    /// </summary>
    public License AddTo( LicenseServerTestContext context )
    {
        string key = $"FAKE-KEY-{this.licenseId}";

        License license = new()
        {
            LicenseId = this.licenseId,
            LicenseKey = key,
            ProductCode = this.product,
            Priority = this.priority,
            CreatedOn = TestClock.Origin,
            GraceStartTime = this.graceStartTime
        };

        context.LicenseParser.Register( key, this.BuildInfo() );
        context.Db.Licenses.Add( license );
        context.Db.SaveChanges();

        return license;
    }
}

/// <summary>
/// Builds a lease directly, without the allocation rules, to create the initial state of a test.
/// </summary>
public sealed class LeaseBuilder
{
    private readonly License license;
    private string userName = "alice";
    private string machine = "desktop-1";
    private DateTime startTime = TestClock.Origin;
    private DateTime endTime = TestClock.Origin.AddDays( 3 );
    private bool grace;

    private LeaseBuilder( License license )
    {
        this.license = license;
    }

    public static LeaseBuilder For( License license ) => new( license );

    public LeaseBuilder User( string value )
    {
        this.userName = value;

        return this;
    }

    public LeaseBuilder Machine( string value )
    {
        this.machine = value;

        return this;
    }

    public LeaseBuilder From( DateTime value )
    {
        this.startTime = value;

        return this;
    }

    public LeaseBuilder To( DateTime value )
    {
        this.endTime = value;

        return this;
    }

    public LeaseBuilder Lasting( double days )
    {
        this.endTime = this.startTime.AddDays( days );

        return this;
    }

    public LeaseBuilder InGrace()
    {
        this.grace = true;

        return this;
    }

    public Lease AddTo( LicenseServerTestContext context )
    {
        Lease lease = new()
        {
            License = this.license,
            LicenseId = this.license.LicenseId,
            UserName = this.userName,
            Machine = this.machine,
            AuthenticatedUser = this.userName,
            StartTime = this.startTime,
            EndTime = this.endTime,
            Grace = this.grace
        };

        // The lease is saved through the repository, so that it is signed as a real lease is.
        context.Db.Leases.Add( lease );
        context.Repository.SaveChanges();

        return lease;
    }
}
