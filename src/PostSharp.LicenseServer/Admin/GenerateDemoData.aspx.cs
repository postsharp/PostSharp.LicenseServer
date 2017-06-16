#region Copyright (c) 2004-2010 by SharpCrafters s.r.o.

// This file is part of PostSharp source code and is the property of SharpCrafters s.r.o.
// 
// Source code is provided to customers under strict non-disclosure agreement (NDA). 
// YOU MUST HAVE READ THE NDA BEFORE HAVING RECEIVED ACCESS TO THIS SOURCE CODE. 
// Severe financial penalties apply in case of non respect of the NDA.

#endregion

using System;
using System.Collections.Generic;
using System.IO;
using System.Web.UI;
using ParsedLicense = PostSharp.Sdk.Extensibility.Licensing.License;

namespace PostSharp.LicenseServer.Admin
{
    public partial class GenerateDemoData : Page
    {
        private const string unparsedNames =
            @"David,Rahm
Jimmy,Smith
Carroll,Lash
Keith,Smith
Wm,Martinez
Marsha,Bassham
Mike,Bergeron
Julio,Bayliss
Salvatore,Nail
Herbert,Thompson
Keith,Gentile
Gary,Turner
Jesse,Stinson
Louis,Callender
Duane,Rudder
Joan,Lowrey
Thomas,Bourdeau
Richard,Vega
Charles,Numbers
Paul,Cooper
Albert,Speier
Jerry,Johnson
Sidney,Painter
John,Denton
Monica,Grise
William,Davis
Miguel,Collins
Melanie,Johnson
Nida,Perez
George,Young
Gary,Wilkes
Thomas,Stoneburner
Richard,Bushong
Edmund,Davis
Michael,Smart
Dena,Anderson
Bobbie,Sherman
Ray,Roberts
Francisco,Linck
Jeremy,Thompson
Hubert,Nunnally
Marshall,Hoffman
Stephen,Montes
Wilfred,Cassel
Matthew,Sease
Edward,Johnson
Timothy,Hassell
Brian,Jones
Israel,West
Jesse,Bombard
Don,Tann
Brian,Stringer
Marilyn,Belanger
Donald,Miller
Patrick,Clark
Milton,Cranston
Russell,Christensen
Bill,Piper
Chester,Bennett
Dean,Mcgrath
Dennis,Ortiz
Paul,Arno
Manuel,Cole
Felix,Johnson
Nancy,Mccormick
Robert,Callan
Bernard,Vanwyk
Kelly,Gary
Robert,Murphy
Quincy,Grossman
Richard,Brown
Theodore,Flemming
Christopher,Young
David,Riddle
Donnie,Comstock
Bryan,Hartsock
Timothy,Villarreal
Troy,Thompson
Frederick,Johnson
Joseph,Davis
Christopher,Ayala
Cyrus,Smith
Thomas,Johnson
Monica,Tobin
Kyle,Pierce
Ginger,Miller
John,Smith
Michael,Hines
William,Hansen
Joseph,Hurlburt
Stephen,Green
Noe,Houle
Troy,Lofton
Kristofer,Booth
Joseph,Hoffman
Larry,Mcknight
Brian,Watson
Tom,Greenwood
Cory,Ryals
David,Bradway";


        private readonly Random random = new Random();

        protected void Button1_Click( object sender, EventArgs e )
        {
            Database db = new Database();

            LeaseService leaseService = new LeaseService( false );
            string product = this.ProductTextBox.Text;
            Version version = Version.Parse( this.VersionTextBox.Text );
            int maxUsers = int.Parse( this.CountTextBox.Text );

            StringReader reader = new StringReader( unparsedNames );
            List<User> users = new List<User>();


            string line;
            while ( (line = reader.ReadLine()) != null )
            {
                string[] parts = line.Split( ',' );

                User user = new User
                                {
                                    UserName = string.Format( "{0}.{1}", parts[0], parts[1] ).ToUpper(),
                                    AuthenticatedName = string.Format( "CONTOSO\\{0}.{1}", parts[0], parts[1] ).ToUpper(),
                                    WorksOnWeekend = random.NextDouble()
                                };
                double machineProb = random.NextDouble();

                if ( machineProb < 0.7 )
                {
                    user.Machines.Add( string.Format( "DESKTOP-{0:x}", random.Next( 0, ushort.MaxValue ) ) );
                }
                else
                {
                    user.Machines.Add( string.Format( "DESKTOP-{0:x}", random.Next( 0, ushort.MaxValue ) ) );
                    user.Machines.Add( string.Format( "NOTEBOOK-{0:x}", random.Next( 0, ushort.MaxValue ) ) );
                }

                users.Add( user );
            }

            const int days = 90;
            DateTime startDate = DateTime.Today.AddDays( -days );
            int numberNewUsersPerDay = (int) Math.Ceiling( 2*random.NextDouble()*maxUsers/days );

            List<User> activeUsers = new List<User>();
            for ( int i = 0; i < days; i++ )
            {
                DateTime day = startDate.AddDays( i );
                DateTime time = day.AddHours( 7 );

                List<User> removeList = new List<User>();
                foreach ( User user in activeUsers )
                {
                    double probWorksToday;
                    switch ( day.DayOfWeek )
                    {
                        case DayOfWeek.Sunday:
                            probWorksToday = user.WorksOnWeekend*0.3;
                            break;
                        case DayOfWeek.Saturday:
                            probWorksToday = user.WorksOnWeekend*0.7;
                            break;
                        default:
                            probWorksToday = 0.9;
                            break;
                    }

                    if ( random.NextDouble() < probWorksToday )
                    {
                        // Which machine will he use this day?
                        int machineIndex = (int) Math.Floor( random.NextDouble()*user.Machines.Count );
                        string machine = user.Machines[machineIndex];
                        Dictionary<int, string> errors = new Dictionary<int, string>();
                        time = time.AddHours( random.NextDouble()*3.0/activeUsers.Count );
                        DateTime buildDate = time;
                        Lease lease = leaseService.GetLease( db, product, version, buildDate, machine, user.UserName, user.AuthenticatedName, time, errors );
                        if ( lease != null )
                        {
                            db.SubmitChanges();
                            LeaseService.CheckAssertions( db, lease.License, time );
                        }
                    }

                    // Will this user stop using the product?
                    if ( random.NextDouble()*activeUsers.Count < 0.02 )
                        removeList.Add( user );
                }

                // Enlist new users.


                for ( int j = 0; j < numberNewUsersPerDay; j++ )
                {
                    if ( users.Count > 0 )
                    {
                        activeUsers.Add( users[0] );
                        users.RemoveAt( 0 );
                    }
                }

                // Remove users.
                foreach ( User user in removeList )
                {
                    activeUsers.Remove( user );
                    users.Add( user );
                }
            }
        }

        private new class User
        {
            public string UserName;
            public string AuthenticatedName;
            public double WorksOnWeekend;
            public readonly List<string> Machines = new List<string>();
        }
    }
}