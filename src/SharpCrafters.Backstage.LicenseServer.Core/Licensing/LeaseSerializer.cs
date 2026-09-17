using System.Xml;

namespace SharpCrafters.Backstage.LicenseServer.Licensing;

/// <summary>
/// Serializes a lease in the format the client parses.
/// </summary>
/// <remarks>
/// <para>
/// The format is the one PostSharp's <c>LicenseLease.Serialize</c> produced and the one
/// <c>SharpCrafters.Backstage.Licensing.LicenseServer.LicenseLease.TryDeserialize</c> reads: the
/// four parts separated by <c>"; "</c>, each named and followed by a colon, with the instants in the
/// XML round-trip representation of UTC. It is written here rather than called, because the type of
/// Backstage is internal to that package and has no serializer -- a client only ever reads a lease.
/// </para>
/// <para>
/// Every deployed client parses this, so the shape is a contract and is pinned by a test. The
/// parsing on the client side is lenient -- parts are matched by name without regard to case and an
/// unknown part is ignored -- so a later version of the server may add a part, but may not rename or
/// reorder one.
/// </para>
/// </remarks>
public sealed class LeaseSerializer : ILeaseSerializer
{
    public string Serialize( string licenseKey, DateTime startTime, DateTime endTime, DateTime renewTime )
        => $"License: {licenseKey}"
           + $"; StartTime: {ToUtcString( startTime )}"
           + $"; EndTime: {ToUtcString( endTime )}"
           + $"; RenewTime: {ToUtcString( renewTime )}";

    /// <remarks>
    /// <see cref="XmlDateTimeSerializationMode.Utc"/> and not <see cref="XmlDateTimeSerializationMode.RoundtripKind"/>:
    /// the times come from the database, where a <c>datetime</c> has no time zone, and the mode has
    /// to be the one that reads an unspecified instant as UTC rather than as local time. The lease
    /// times this server produces are already tagged as UTC, so the two agree -- but a caller that
    /// hands over an untagged instant gets the right answer as well.
    /// </remarks>
    private static string ToUtcString( DateTime value )
        => XmlConvert.ToString( value, XmlDateTimeSerializationMode.Utc );
}
