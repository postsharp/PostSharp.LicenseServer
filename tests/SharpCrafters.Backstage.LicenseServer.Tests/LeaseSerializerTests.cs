using SharpCrafters.Backstage.LicenseServer.Licensing;

namespace SharpCrafters.Backstage.LicenseServer.Tests;

/// <summary>
/// The body of a lease response. Every deployed client parses it, so it is pinned against a literal
/// expected string rather than against a re-implementation of the same formatting.
/// </summary>
/// <remarks>
/// The expected values are the ones the <c>LicenseLease.Serialize</c> of the PostSharp SDK produced,
/// so that a client that was talking to the previous version of this server sees no change.
/// </remarks>
public sealed class LeaseSerializerTests
{
    private static readonly LeaseSerializer serializer = new();

    private static readonly DateTime start = new( 2026, 1, 5, 9, 0, 0, DateTimeKind.Utc );

    [Fact]
    public void Serialize_ProducesTheExpectedBody()
        => Assert.Equal(
            "License: 1-ABCDEF"
            + "; StartTime: 2026-01-05T09:00:00Z"
            + "; EndTime: 2026-01-08T09:00:00Z"
            + "; RenewTime: 2026-01-07T09:00:00Z",
            serializer.Serialize( "1-ABCDEF", start, start.AddDays( 3 ), start.AddDays( 2 ) ) );

    /// <summary>
    /// A time read from a <c>datetime</c> column carries no kind. It is a UTC instant all the same,
    /// and must not be shifted by the time zone the server happens to keep.
    /// </summary>
    [Fact]
    public void Serialize_TreatsAnUntaggedTimeAsUtc()
    {
        DateTime unspecified = new( 2026, 1, 5, 9, 0, 0, DateTimeKind.Unspecified );

        Assert.Equal(
            serializer.Serialize( "1-ABCDEF", start, start, start ),
            serializer.Serialize( "1-ABCDEF", unspecified, unspecified, unspecified ) );
    }

    /// <summary>
    /// The client splits the body on <c>;</c> and each part at its first <c>:</c>, so a key may not
    /// contain either character. A license key is an identifier, a hyphen and Base32, so it never
    /// does -- this pins the assumption rather than the behaviour.
    /// </summary>
    [Fact]
    public void Serialize_ProducesFourPartsTheClientCanSplit()
    {
        string[] parts = serializer.Serialize( "1-ABCDEF", start, start, start ).Split( ';' );

        Assert.Equal( 4, parts.Length );
        Assert.Equal( ["License", "StartTime", "EndTime", "RenewTime"], parts.Select( p => p[..p.IndexOf( ':' )].Trim() ) );
    }
}
