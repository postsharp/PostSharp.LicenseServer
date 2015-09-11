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
using ParsedLicense = PostSharp.Sdk.Extensibility.Licensing.License;

namespace PostSharp.LicenseServer
{
    public partial class DefaultPage : Page
    {
        protected override void OnLoad( EventArgs e )
        {
            Database db = new Database();
            License[] licenses = (from l in db.Licenses orderby l.ProductCode , l.Priority select l).ToArray();
            List<LicenseInfo> licenseInfos = new List<LicenseInfo>();

            for ( int i = 0; i < licenses.Length; i++ )
            {
                License license = licenses[i];
                LicenseInfo licenseInfo = new LicenseInfo {LicenseId = license.LicenseId};
                ParsedLicense parsedLicense = ParsedLicenseManager.GetParsedLicense( license.LicenseKey );
                if ( parsedLicense == null )
                {
                    licenseInfo.LicenseType = "INVALID";
                }
                else
                {
                    licenseInfo.LicenseType = parsedLicense.LicenseType.ToString();
                    licenseInfo.MaxUsers = parsedLicense.UserNumber;
                    licenseInfo.CurrentUsers = db.GetActiveLeads( license.LicenseId, VirtualDateTime.UtcNow );
                    licenseInfo.ProductCode = parsedLicense.Product.ToString();
                    licenseInfo.GraceStartTime = license.GraceStartTime;
                }

                licenseInfos.Add( licenseInfo );
            }

            this.noLicensePanel.Visible = licenseInfos.Count == 0;
            this.GridView1.DataSource = licenseInfos;
            this.GridView1.DataBind();
        }

        private class LicenseInfo
        {
            public int LicenseId { get; set; }
            public string LicenseType { get; set; }
            public string ProductCode { get; set; }
            public int? MaxUsers { get; set; }
            public int CurrentUsers { get; set; }
            public DateTime? GraceStartTime { get; set; }
        }
    }
}