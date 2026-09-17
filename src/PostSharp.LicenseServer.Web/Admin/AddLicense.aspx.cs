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
using PostSharp.Sdk;
using PostSharp.Sdk.Extensibility.Licensing;
using ParsedLicense = PostSharp.Sdk.Extensibility.Licensing.License;

namespace PostSharp.LicenseServer.Admin
{
    public partial class AddLicensePage : Page
    {
        protected void AddButton_OnClick( object sender, EventArgs e )
        {
            ParsedLicense parsedLicense = ParsedLicense.Deserialize( this.licenseKeyTextBox.Text );

            if ( parsedLicense == null )
            {
                this.errorLabel.Text = "Invalid license key.";
                this.errorLabel.Visible = true;
                return;
            }

            if ( !parsedLicense.IsLicenseServerEligible() )
            {
                    this.errorLabel.Text = string.Format( "Cannot add a {0} of {1} to the server.", 
                        parsedLicense.GetLicenseTypeName() ?? "(unknown license type)", 
                        parsedLicense.GetProductName( ) );
                    this.errorLabel.Visible = true;
                    return;
            }

            License license = new License
                                  {
                                      LicenseId = parsedLicense.LicenseId,
                                      LicenseKey = ParsedLicense.CleanLicenseString( this.licenseKeyTextBox.Text ),
                                      CreatedOn = VirtualDateTime.UtcNow,
                                      ProductCode = parsedLicense.Product.ToString(),
                                  };

            Database db = new Database();
            if (db.Licenses.Any(l => l.LicenseId == license.LicenseId))
            {
                this.errorLabel.Text = "The given license has been added already.";
                this.errorLabel.Visible = true;
                return;
            }

            db.Licenses.InsertOnSubmit(license);
            db.SubmitChanges();

            this.Response.Redirect( ".." );
        }
    }
}