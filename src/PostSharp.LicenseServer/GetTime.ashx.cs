using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;
using System.Xml;

namespace PostSharp.LicenseServer
{
    /// <summary>
    /// Summary description for GetTime
    /// </summary>
    public class GetTime : IHttpHandler
    {

        public void ProcessRequest(HttpContext context)
        {
            context.Response.ContentType = "text/plain";
            context.Response.Write(XmlConvert.ToString(VirtualDateTime.UtcNow, XmlDateTimeSerializationMode.Utc));
            context.Response.Write(";");
            context.Response.Write(XmlConvert.ToString(VirtualDateTime.Acceleration));
        }

        public bool IsReusable
        {
            get
            {
                return true;
            }
        }
    }
}