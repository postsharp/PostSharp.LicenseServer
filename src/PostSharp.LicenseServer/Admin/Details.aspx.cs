#region Copyright (c) 2004-2010 by SharpCrafters s.r.o.

// This file is part of PostSharp source code and is the property of SharpCrafters s.r.o.
// 
// Source code is provided to customers under strict non-disclosure agreement (NDA). 
// YOU MUST HAVE READ THE NDA BEFORE HAVING RECEIVED ACCESS TO THIS SOURCE CODE. 
// Severe financial penalties apply in case of non respect of the NDA.

#endregion

using System;
using System.Collections.Generic;
using System.Linq;
using System.Web.UI;

namespace PostSharp.LicenseServer.Admin
{
    public partial class DetailsPage : Page
    {
        protected override void OnLoad( EventArgs e )
        {
            int licenseId = int.Parse( this.Request.QueryString["id"] );
            this.licenseIdLabel.Text = licenseId.ToString();

            Database db = new Database();

            DateTime now = VirtualDateTime.UtcNow;
            List<Lease> leases = (from l in db.Leases
                                  join Lease o in db.Leases on l.LeaseId equals o.OverwrittenLeaseId into o
                                  from oo in o.DefaultIfEmpty()
                                  where
                                      oo == null &&
                                      l.LicenseId == licenseId &&
                                      l.StartTime <= now &&
                                      l.EndTime >= now
                                  orderby l.StartTime
                                  select l).ToList();


            this.GridView1.DataSource = leases;
            this.GridView1.DataBind();

            this.leaseCountLiteral.Text = leases.Count.ToString();
            this.concurrentUserCountLiteral.Text = db.GetActiveLeads( licenseId, now ).ToString();
        }
    }
}