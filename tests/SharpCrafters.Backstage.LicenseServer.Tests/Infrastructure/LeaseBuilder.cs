// Copyright (c) SharpCrafters s.r.o. See the LICENSE.md file in the root directory of this repository root for details.

using SharpCrafters.Backstage.LicenseServer.Licensing;

namespace SharpCrafters.Backstage.LicenseServer.Tests.Infrastructure;

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