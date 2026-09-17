using System.Xml;
using SharpCrafters.Backstage.LicenseServer.Security;

namespace SharpCrafters.Backstage.LicenseServer;

public partial class Lease
{
    /// <summary>
    /// Writes the audit-log representation of this lease: a semicolon-separated line in which the
    /// machine and user names appear only as hashes, so the log can be shared without disclosing who
    /// works where.
    /// </summary>
    /// <remarks>
    /// This is a serialization contract. Timestamps are written as UTC; the entity's
    /// <see cref="DateTime"/> values are tagged as UTC when they are read from the database, without
    /// which <see cref="XmlConvert"/> would treat them as local time and shift them.
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
    /// Returns the audit-log representation of this lease.
    /// </summary>
    public string ToAuditLine( bool includeHmac )
    {
        StringWriter writer = new();
        this.Write( writer, includeHmac );

        return writer.ToString();
    }
}
