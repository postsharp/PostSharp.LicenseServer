#region Copyright (c) 2004-2010 by SharpCrafters s.r.o.

// This file is part of PostSharp source code and is the property of SharpCrafters s.r.o.
// 
// Source code is provided to customers under strict non-disclosure agreement (NDA). 
// YOU MUST HAVE READ THE NDA BEFORE HAVING RECEIVED ACCESS TO THIS SOURCE CODE. 
// Severe financial penalties apply in case of non respect of the NDA.

#endregion

using System;
using System.Linq;
using System.Web.UI;

namespace PostSharp.LicenseServer.Admin
{
    public partial class CancelPage : Page
    {
        private Database db;
        private Lease lease;

        protected override void OnInit( EventArgs e )
        {
            int leaseId = int.Parse( this.Request.QueryString["id"] );
            this.db = new Database();
            this.lease = (from l in db.Leases where l.LeaseId == leaseId select l).Single();
        }

        protected void yesButton_OnClick( object sender, EventArgs e )
        {
            db.CancelLease( lease, this.User.Identity.Name, VirtualDateTime.UtcNow );
            db.SubmitChanges();
            this.Response.Redirect( "Details.aspx?id=" + lease.LicenseId );
        }

        protected void noButton_OnClick( object sender, EventArgs e )
        {
            this.Response.Redirect( "Details.aspx?id=" + lease.LicenseId );
        }
    }
}