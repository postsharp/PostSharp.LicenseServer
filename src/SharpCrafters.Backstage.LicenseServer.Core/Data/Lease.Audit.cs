using System.Xml;
using SharpCrafters.Backstage.LicenseServer.Security;

namespace SharpCrafters.Backstage.LicenseServer;

public partial class Lease
{
    /// <summary>
    /// Writes this lease as a line of the audit log. The fields are separated by semicolons, and the
    /// machine name and the user name appear only as hashes, so that the log can be shared without
    /// disclosing who works where.
    /// </summary>
    /// <remarks>
    /// This format is a serialization contract. The timestamps are written as UTC. The
    /// <see cref="DateTime"/> values of the entity receive the UTC kind when they are read from the
    /// database. Without that kind, <see cref="XmlConvert"/> would treat them as local times and
    /// shift them.
    /// </remarks>
    public void Write( TextWriter textWriter, bool includeHmac )
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
        textWriter.Write( StringHash.ComputeStringHash64( this.Machine ).ToString( "x" ) );
        textWriter.Write( ';' );
        textWriter.Write( StringHash.ComputeStringHash64( this.UserName ).ToString( "x" ) );

        if ( includeHmac )
        {
            textWriter.Write( ';' );
            textWriter.Write( this.HMAC );
        }
    }

    /// <summary>
    /// Returns this lease as a line of the audit log.
    /// </summary>
    public string ToAuditLine( bool includeHmac )
    {
        StringWriter writer = new();
        this.Write( writer, includeHmac );

        return writer.ToString();
    }
}
