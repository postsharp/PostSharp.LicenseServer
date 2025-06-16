using PostSharp.Platform.Neutral;
using PostSharp.Sdk.Extensibility;
using PostSharp.Sdk.Extensibility.Licensing;
using PostSharp.Sdk.User;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Text;
using System.Threading;
using System.Xml;

namespace SharpCrafters.LicenseServer.Test
{
    class Program
    {
        static readonly Random random = new Random();
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

        public static string Url = "http://localhost:44670/";

        private static void OnTime( object state )
        {
            Console.WriteLine(VirtualDateTime.UtcNow.ToLocalTime());
        }

        static void Main(string[] args)
        {
            if ( args.Length > 0 )
                Url = args[0];

            CommonDefaultSystemServices.Initialize();
            Messenger.Initialize();
            Messenger.Current.Message += ( sender, eventArgs ) => Console.WriteLine( VirtualDateTime.UtcNow.ToLocalTime().ToString() + ": " +
                eventArgs.Message.MessageText );
        
            FixVirtualTime();

            List<User> users = GetUsers(75, 2, 2);

            foreach ( User user in users)
            {
                StartUserSimulation( user );
            }

            new Thread( PrintTime ).Start();

        }

        private static void PrintTime()
        {
            while ( true )
            {
                Thread.Sleep( TimeSpan.FromSeconds( 5 ) );
                Console.WriteLine("Current time: {0}", VirtualDateTime.UtcNow.ToLocalTime() );
            }
        }

        public static void FixVirtualTime()
        {
            string getTimeUrl = Url + "GetTime.ashx";
            WebClient webClient = new WebClient();
            string[] remoteTime = webClient.DownloadString( getTimeUrl ).Split( ';' );
            VirtualDateTime.Initialize( XmlConvert.ToDateTime( remoteTime[0], XmlDateTimeSerializationMode.Utc ), XmlConvert.ToDecimal( remoteTime[1] ) );
        }

        private static void StartUserSimulation( User user )
        {
            ClientSimulator clientSimulator = new ClientSimulator( user );
            Thread thread = new Thread( clientSimulator.Main )
            {
                Name = string.Format( "Simulation for user {0} ", user )
            };

            thread.Start();
        }

        private static List<User> GetUsers(int userCount, int buildServerUserCount, int buildServerMachineCount)
        {
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

                if ( users.Count >= userCount )
                    break;
            }

            for ( int i = 0; i < buildServerUserCount; i++ )
            {
                User user = new User
                {
                    UserName = string.Format("BUILDSERVER.{0}", i),
                    AuthenticatedName = string.Format("CONTOSO\\BUILDSERVER.{0}", i),
                    WorksOnWeekend = 1
                };

                for ( int j = 0; j < buildServerMachineCount; j++ )
                {
                    user.Machines.Add(string.Format("SERVER-{0:x}", random.Next( 0, ushort.MaxValue )));
                }

                users.Add(user);
            }

            return users;
        }

       
    }
}
