#region Copyright (c) 2004-2010 by SharpCrafters s.r.o.

// This file is part of PostSharp source code and is the property of SharpCrafters s.r.o.
// 
// Source code is provided to customers under strict non-disclosure agreement (NDA). 
// YOU MUST HAVE READ THE NDA BEFORE HAVING RECEIVED ACCESS TO THIS SOURCE CODE. 
// Severe financial penalties apply in case of non respect of the NDA.

#endregion

using System;
using System.IO;
using System.Linq;
using System.Web;

namespace PostSharp.LicenseServer.Admin
{
    /// <summary>
    /// Summary description for Export
    /// </summary>
    public class ExportHandler : IHttpHandler
    {
        public void ProcessRequest( HttpContext context )
        {
            int fromYear = int.Parse( context.Request.QueryString["fy"] );
            int toYear = int.Parse( context.Request.QueryString["ty"] );
            int fromMonth = int.Parse( context.Request.QueryString["fm"] );
            int toMonth = int.Parse( context.Request.QueryString["tm"] );

            context.Response.ContentType = "text/plain";
            context.Response.AddHeader( "Content-Disposition", string.Format( "attachment; filename=PostSharp_LicenseLog_{0}-{1}_{2}-{3}.txt",
                                                                              fromYear, fromMonth, toYear, toMonth ) );


            DateTime fromTime = new DateTime( fromYear, fromMonth, 1 );
            DateTime toTime = new DateTime( toYear, toMonth, 1 ).AddMonths( 1 );

            Database db = new Database();
            var bounds = (from l in db.Leases
                          where l.EndTime >= fromTime && l.StartTime <= toTime
                          group l by 0
                          into g
                          select new {MinLeaseId = g.Min( ll => ll.LeaseId ), MaxLeaseId = g.Max( ll => ll.LeaseId )}).SingleOrDefault();

            if ( bounds != null )
            {
                StreamWriter writer = new StreamWriter( context.Response.OutputStream );

                IOrderedQueryable<Lease> query = from l in db.Leases
                                                 where l.LeaseId >= bounds.MinLeaseId && l.LeaseId <= bounds.MaxLeaseId
                                                 orderby l.LeaseId
                                                 select l;

                foreach ( Lease lease in query )
                {
                    lease.Write( writer, true );
                    writer.WriteLine();
                }

                writer.Flush();
            }
        }

        public bool IsReusable
        {
            get { return true; }
        }
    }
}