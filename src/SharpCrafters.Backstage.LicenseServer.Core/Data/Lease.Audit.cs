using System.Xml;

namespace SharpCrafters.Backstage.LicenseServer;

public partial class Lease
{
    /// <summary>
    /// Writes this lease as a line of the audit log. The fields are separated by semicolons, and the
    /// machine name and the user name are written as they are stored.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The log is read by the administrator of the server, who needs to know which user and which
    /// machine hold a seat. A version earlier than 2027.0 wrote the two names as hashes. Nothing
    /// could read such a log, and the file was exported to be read.
    /// </para>
    /// <para>
    /// The file therefore contains personal data. It is meant for the organization that runs the
    /// server, and not for PostSharp Technologies.
    /// </para>
    /// <para>
    /// This format is a serialization contract. The timestamps are written as UTC. The
    /// <see cref="DateTime"/> values of the entity receive the UTC kind when they are read from the
    /// database. Without that kind, <see cref="XmlConvert"/> would treat them as local times and
    /// shift them.
    /// </para>
    /// </remarks>
    public void Write( TextWriter textWriter )
    {
        ArgumentNullException.ThrowIfNull( textWriter );

        textWriter.Write( this.LeaseId );
        textWriter.Write( ';' );
        textWriter.Write( this.OverwrittenLeaseId );
        textWriter.Write( ';' );
        textWriter.Write( this.LicenseId );
        textWriter.Write( ';' );
        textWriter.Write( XmlConvert.ToString( this.StartTime, XmlDateTimeSerializationMode.RoundtripKind ) );
        textWriter.Write( ';' );
        textWriter.Write( XmlConvert.ToString( this.EndTime, XmlDateTimeSerializationMode.RoundtripKind ) );
        textWriter.Write( ';' );
        textWriter.Write( this.Machine );
        textWriter.Write( ';' );
        textWriter.Write( this.UserName );
    }

    /// <summary>
    /// Returns this lease as a line of the audit log.
    /// </summary>
    public string ToAuditLine()
    {
        StringWriter writer = new();
        this.Write( writer );

        return writer.ToString();
    }
}
