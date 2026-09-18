using System.Xml;

namespace SharpCrafters.Backstage.LicenseServer.Licensing;

/// <summary>
/// Serializes a lease in the format the client parses.
/// </summary>
/// <remarks>
/// <para>
/// The format is the format that <c>LicenseLease.Serialize</c> of PostSharp produced, and that
/// <c>SharpCrafters.Backstage.Licensing.LicenseServer.LicenseLease.TryDeserialize</c> reads. It has
/// four parts separated by <c>"; "</c>. Each part carries a name followed by a colon, and each
/// instant is written in the XML round-trip representation of UTC. The serializer is written here
/// and not called, because the type of Backstage is internal to that package and has no serializer.
/// A client only reads a lease.
/// </para>
/// <para>
/// Every deployed client parses this format, so the format is a contract, and a test verifies it.
/// The client parses it leniently: it matches the parts by name without regard to case, and it
/// ignores a part it does not know. A later version of the server can therefore add a part, but it
/// cannot rename one.
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
    /// The mode is <see cref="XmlDateTimeSerializationMode.Utc"/> and not
    /// <see cref="XmlDateTimeSerializationMode.RoundtripKind"/>. The instants come from the database,
    /// where a <c>datetime</c> has no time zone, and this mode reads an unspecified instant as UTC
    /// and not as a local time. The instants that this server produces already carry the UTC kind,
    /// so both modes give the same result for them. This mode also gives the correct result for a
    /// caller that passes an instant without a kind.
    /// </remarks>
    private static string ToUtcString( DateTime value )
        => XmlConvert.ToString( value, XmlDateTimeSerializationMode.Utc );
}
