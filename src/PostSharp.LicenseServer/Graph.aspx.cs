#region Copyright (c) 2004-2010 by SharpCrafters s.r.o.

// This file is part of PostSharp source code and is the property of SharpCrafters s.r.o.
// 
// Source code is provided to customers under strict non-disclosure agreement (NDA). 
// YOU MUST HAVE READ THE NDA BEFORE HAVING RECEIVED ACCESS TO THIS SOURCE CODE. 
// Severe financial penalties apply in case of non respect of the NDA.

#endregion

using System;
using System.Globalization;
using System.Linq;
using System.Web.UI;
using ParsedLicense = PostSharp.Sdk.Extensibility.Licensing.License;

namespace PostSharp.LicenseServer
{
    public partial class Graph : Page
    {
        protected string[] Keys;
        protected string[] Values;
        protected int? Maximum;
        protected int? GraceMaximum;
        protected int LicenseId;
        protected int Days;
        protected int AxisMaximum;

        protected override void OnLoad(EventArgs e)
        {
            Database db = new Database();
          

            LicenseId = int.Parse(this.Request.QueryString["id"]);

            Days = int.Parse( this.Request.QueryString["days"] ?? "30" );

            DateTime endDate = VirtualDateTime.UtcNow.Date.AddDays(1);
            DateTime startDate = endDate.AddDays( -Days );

            // Retrieve license information.
            License license = (from l in db.Licenses where l.LicenseId == LicenseId select l).Single();
            ParsedLicense parsedLicense = ParsedLicense.Deserialize(license.LicenseKey);
            if (parsedLicense != null)
            {
                if (parsedLicense.UserNumber.HasValue)
                {
                    this.Maximum = parsedLicense.UserNumber;
                    this.GraceMaximum = this.Maximum.GetValueOrDefault()*(100 + parsedLicense.GetGracePercentOrDefault())/100;
                    this.AxisMaximum = GraceMaximum.GetValueOrDefault();
                }
            }
            

            var q = from l in db.GetLeaseCountingPoints( LicenseId, startDate, endDate )
                    group l by l.Time.Date
                    into d select new { Date = d.Key, Count = d.Max(point => point.LeaseCount) };

            // Fill the array with data.
            this.Keys = new string[Days];
            this.Values = new string[Days];
            foreach ( var point in q )
            {
                int day = (int) Math.Floor( point.Date.Subtract( startDate ).TotalDays );

                if ( point.Count > this.AxisMaximum )
                {
                    this.AxisMaximum = point.Count;
                }

                if ( day < 0 )
                {
                    this.Values[0] = point.Count.ToString();
                }
                else if ( day < Days )
                {
                    this.Values[day] = point.Count.ToString();
                }
            }

            // Do extrapolation of missing values.
            for ( int i = 0; i < Days; i++ )
            {
                DateTime date = startDate.AddDays( i    );
                if (date.DayOfWeek == DayOfWeek.Monday)
                {
                    this.Keys[i] = date.ToString( "MM/dd/yyyy", CultureInfo.InvariantCulture );
                }
                if ( this.Values[i] == null )
                {
                    if (i > 0)
                    {
                        this.Values[i] = this.Values[i - 1] ?? "0";
                    }
                    else
                    {
                        this.Values[0] = "0";
                    }
                }
            }


            this.AxisMaximum = (int) Math.Ceiling( Math.Ceiling( this.AxisMaximum*1.2 )/10)*10;
        }
    }
}