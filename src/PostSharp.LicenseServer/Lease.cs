#region Copyright (c) 2004-2010 by SharpCrafters s.r.o.

// This file is part of PostSharp source code and is the property of SharpCrafters s.r.o.
// 
// Source code is provided to customers under strict non-disclosure agreement (NDA). 
// YOU MUST HAVE READ THE NDA BEFORE HAVING RECEIVED ACCESS TO THIS SOURCE CODE. 
// Severe financial penalties apply in case of non respect of the NDA.

#endregion

using System.IO;
using System.Xml;
using PostSharp.Sdk.Extensibility.Licensing;

namespace PostSharp.LicenseServer
{
    partial class Lease
    {
        public void Write( TextWriter textWriter, bool includeHmac )
        {
            textWriter.Write( this.LeaseId );
            textWriter.Write( ';' );
            textWriter.Write( this.OverwrittenLeaseId );
            textWriter.Write( ';' );
            textWriter.Write( this.LicenseId );
            textWriter.Write( ';' );
            textWriter.Write( XmlConvert.ToString( this.StartTime, XmlDateTimeSerializationMode.Utc ) );
            textWriter.Write( ';' );
            textWriter.Write( XmlConvert.ToString( this.EndTime, XmlDateTimeSerializationMode.Utc ) );
            textWriter.Write( ';' );
            textWriter.Write( CryptoUtilities.ComputeStringHash64( this.Machine ).ToString( "x" ) );
            textWriter.Write( ';' );
            textWriter.Write( CryptoUtilities.ComputeStringHash64( this.UserName ).ToString( "x" ) );
            if ( includeHmac )
            {
                textWriter.Write( ';' );
                textWriter.Write( this.HMAC );
            }
        }
    }
}