#region Copyright (c) 2004-2010 by SharpCrafters s.r.o.

// This file is part of PostSharp source code and is the property of SharpCrafters s.r.o.
// 
// Source code is provided to customers under strict non-disclosure agreement (NDA). 
// YOU MUST HAVE READ THE NDA BEFORE HAVING RECEIVED ACCESS TO THIS SOURCE CODE. 
// Severe financial penalties apply in case of non respect of the NDA.

#endregion

using System;
using System.Web.UI;

namespace PostSharp.LicenseServer.Admin
{
    public partial class ExportPage : Page
    {
        protected override void OnLoad( EventArgs e )
        {
            if ( !this.IsPostBack )
            {
                DateTime lastMonth = VirtualDateTime.UtcNow.AddMonths( -1 );
                this.fromYearTextBox.Text = this.toYearTextBox.Text = lastMonth.Year.ToString();
                this.fromMonthDropDownList.SelectedIndex = this.toMonthDropDownList.SelectedIndex = (lastMonth.Month - 1);
            }
        }

        protected void downloadButton_OnClick( object sender, EventArgs e )
        {
            if ( this.IsValid )
            {
                this.Response.Redirect( string.Format( "Export.ashx?fy={0}&fm={1}&ty={2}&tm={3}",
                                                       this.fromYearTextBox.Text, this.fromMonthDropDownList.SelectedValue,
                                                       this.toYearTextBox.Text, this.toMonthDropDownList.SelectedValue )
                    );
            }
        }
    }
}