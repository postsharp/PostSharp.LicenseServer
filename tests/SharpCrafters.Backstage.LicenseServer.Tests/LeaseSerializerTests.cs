using SharpCrafters.Backstage.LicenseServer.Licensing;

namespace SharpCrafters.Backstage.LicenseServer.Tests;

/// <summary>
/// The body of the response of a lease request. Every deployed client parses it, so these tests
/// compare it to a literal string, and not to a second implementation of the same formatting.
/// </summary>
/// <remarks>
/// The expected values are the ones the <c>LicenseLease.Serialize</c> of the PostSharp SDK produced,
/// so that a client that was talking to the previous version of this server sees no change.
/// </remarks>
public sealed class LeaseSerializerTests
{
    private static readonly DateTime start = new( 2026, 1, 5, 9, 0, 0, DateTimeKind.Utc );

    [Fact]
    public void Serialize_ProducesTheExpectedBody()
        => Assert.Equal(
            "License: 1-ABCDEF"
            + "; StartTime: 2026-01-05T09:00:00Z"
            + "; EndTime: 2026-01-08T09:00:00Z"
            + "; RenewTime: 2026-01-07T09:00:00Z",
            LeaseSerializer.Serialize( "1-ABCDEF", start, start.AddDays( 3 ), start.AddDays( 2 ) ) );

    /// <summary>
    /// An instant read from a <c>datetime</c> column carries no kind. It is a UTC instant, and the
    /// time zone of the server must not shift it.
    /// </summary>
    [Fact]
    public void Serialize_TreatsAnUntaggedTimeAsUtc()
    {
        DateTime unspecified = new( 2026, 1, 5, 9, 0, 0, DateTimeKind.Unspecified );

        Assert.Equal(
            LeaseSerializer.Serialize( "1-ABCDEF", start, start, start ),
            LeaseSerializer.Serialize( "1-ABCDEF", unspecified, unspecified, unspecified ) );
    }

    /// <summary>
    /// The client splits the body at every <c>;</c>, and each part at its first <c>:</c>, so a key
    /// must contain neither character. A license key contains an identifier, a hyphen and Base32
    /// characters, so it contains neither. This test verifies that assumption.
    /// </summary>
    [Fact]
    public void Serialize_ProducesFourPartsTheClientCanSplit()
    {
        string[] parts = LeaseSerializer.Serialize( "1-ABCDEF", start, start, start ).Split( ';' );

        Assert.Equal( 4, parts.Length );
        Assert.Equal( ["License", "StartTime", "EndTime", "RenewTime"], parts.Select( p => p[..p.IndexOf( ':', StringComparison.Ordinal )].Trim() ) );
    }
}
