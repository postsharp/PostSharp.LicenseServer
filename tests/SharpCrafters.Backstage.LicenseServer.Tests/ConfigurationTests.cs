using System.ComponentModel.DataAnnotations;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.Time.Testing;
using SharpCrafters.Backstage.LicenseServer.Licensing;
using SharpCrafters.Backstage.LicenseServer.Options;
using SharpCrafters.Backstage.LicenseServer.Time;
using SharpCrafters.Backstage.LicenseServer.Tests.Fakes;
using SharpCrafters.Backstage.LicenseServer.Tests.Infrastructure;

namespace SharpCrafters.Backstage.LicenseServer.Tests;

/// <summary>
/// Settings validation, the accelerated clock and the license parse cache.
/// </summary>
public sealed class ConfigurationTests
{
    private static ValidateOptionsResult Validate( Action<LicenseServerOptions> configure )
    {
        LicenseServerOptions options = new();
        configure( options );

        return new LicenseServerOptionsValidator().Validate( null, options );
    }

    [Fact]
    public void Defaults_AreValid() => Assert.True( Validate( _ => { } ).Succeeded );

    /// <summary>
    /// A renewal time that is not before the end of the lease makes the client renew on every single
    /// request. The legacy configuration documented this constraint but never enforced it.
    /// </summary>
    [Theory]
    [InlineData( 3, 3 )]
    [InlineData( 5, 3 )]
    public void MinLeaseDaysNotBelowNewLeaseDays_IsRejected( int minLeaseDays, int newLeaseDays )
    {
        ValidateOptionsResult result = Validate(
            o =>
            {
                o.MinLeaseDays = minLeaseDays;
                o.NewLeaseDays = newLeaseDays;
            } );

        Assert.True( result.Failed );
        Assert.Contains( result.Failures!, f => f.Contains( "MinLeaseDays", StringComparison.Ordinal ) );
    }

    [Fact]
    public void NegativeTimeAcceleration_IsRejected()
        => Assert.True( Validate( o => o.TimeAcceleration = -1 ).Failed );

    [Fact]
    public void NonBase64AuditKey_IsRejected()
        => Assert.True( Validate( o => o.AuditHmacKey = "not base64 !!!" ).Failed );

    [Fact]
    public void Base64AuditKey_IsAccepted()
        => Assert.True( Validate( o => o.AuditHmacKey = Convert.ToBase64String( new byte[32] ) ).Succeeded );

    [Theory]
    [InlineData( 0 )]
    [InlineData( -1 )]
    public void MachinesPerUserBelowOne_FailsDataAnnotations( int value )
    {
        LicenseServerOptions options = new() { MachinesPerUser = value };
        List<ValidationResult> results = [];

        Assert.False(
            Validator.TryValidateObject( options, new ValidationContext( options ), results, true ) );
    }

    [Fact]
    public void MutexTimeout_IsExposedAsATimeSpan()
        => Assert.Equal( TimeSpan.FromSeconds( 45 ), new LicenseServerOptions { MutexTimeout = 45 }.MutexTimeoutSpan );

    [Fact]
    public void AcceleratedTimeProvider_MultipliesElapsedTime()
    {
        FakeTimeProvider inner = new( TestClock.Origin );
        AcceleratedTimeProvider accelerated = new( inner, 1440 );

        inner.Advance( TimeSpan.FromSeconds( 1 ) );

        // A second of real time becomes a day of simulated time.
        Assert.Equal( TestClock.Origin.AddMinutes( 24 ), accelerated.GetUtcNow().UtcDateTime );
    }

    [Fact]
    public void AcceleratedTimeProvider_StartsAtTheCurrentTime()
    {
        FakeTimeProvider inner = new( TestClock.Origin );

        Assert.Equal( TestClock.Origin, new AcceleratedTimeProvider( inner, 100 ).GetUtcNow().UtcDateTime );
    }

    [Theory]
    [InlineData( 0 )]
    [InlineData( -2 )]
    public void AcceleratedTimeProvider_RejectsNonPositiveAcceleration( double acceleration )
        => Assert.Throws<ArgumentOutOfRangeException>(
            () => new AcceleratedTimeProvider( TimeProvider.System, acceleration ) );

    [Fact]
    public void CachingLicenseParser_ParsesEachKeyOnlyOnce()
    {
        FakeLicenseParser inner = new();
        inner.Register( "KEY", LicenseBuilder.Default().BuildInfo() );

        CachingLicenseParser cache = new( inner );

        Assert.NotNull( cache.TryParse( "KEY" ) );
        Assert.NotNull( cache.TryParse( "KEY" ) );
        Assert.Equal( 1, inner.ParseCount );
    }

    /// <summary>
    /// The legacy cache returned before storing a failure, so an invalid key was re-parsed on every
    /// request and on every render of the dashboard.
    /// </summary>
    [Fact]
    public void CachingLicenseParser_RemembersThatAKeyIsInvalid()
    {
        FakeLicenseParser inner = new();
        CachingLicenseParser cache = new( inner );

        Assert.Null( cache.TryParse( "BAD" ) );
        Assert.Null( cache.TryParse( "BAD" ) );
        Assert.Equal( 1, inner.ParseCount );
    }

    [Theory]
    [InlineData( "" )]
    [InlineData( "   " )]
    public void CachingLicenseParser_BlankKey_IsRejectedWithoutParsing( string key )
    {
        FakeLicenseParser inner = new();

        Assert.Null( new CachingLicenseParser( inner ).TryParse( key ) );
        Assert.Equal( 0, inner.ParseCount );
    }
}
