// Copyright (c) SharpCrafters s.r.o. See the LICENSE.md file in the root directory of this repository root for details.

using SharpCrafters.Backstage.LicenseServer.Licensing;

namespace SharpCrafters.Backstage.LicenseServer.Tests.Infrastructure;

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
    private Version? minMetalamaVersion;
    private bool isMetalamaProduct;
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

    /// <summary>
    /// Makes this a license of a Metalama product, whose client reads the minimum Metalama version
    /// and not the minimum PostSharp version.
    /// </summary>
    public LicenseBuilder AsMetalamaProduct( string productCode = "MetalamaProfessional" )
    {
        this.isMetalamaProduct = true;
        this.product = productCode;

        return this;
    }

    public LicenseBuilder WithMinMetalamaVersion( Version? value )
    {
        this.minMetalamaVersion = value;

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
            MinMetalamaVersion = this.minMetalamaVersion,
            IsMetalamaProduct = this.isMetalamaProduct,
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
        var key = $"FAKE-KEY-{this.licenseId}";

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