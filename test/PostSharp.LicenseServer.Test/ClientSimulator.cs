using System;
using System.Diagnostics;
using System.Threading;
using Microsoft.Win32;
using PostSharp.Sdk;
using PostSharp.Sdk.Extensibility.Licensing;

namespace SharpCrafters.LicenseServer.Test
{
    
    class ClientSimulator
    {
        readonly Random random = new Random();
        private readonly User user;
        private static int waitingUsers;

        public ClientSimulator( User user )
        {
            this.user = user;
        }

        public void Main()
        {

            Guid guid = Guid.NewGuid();

            MessageSink messageSink = new MessageSink();

            RegistryKey registryKey = Registry.CurrentUser.CreateSubKey( string.Format( "SOFTWARE\\SharpCrafters\\PostSharp\\LicenseClient\\{0}", guid) );

            using ( registryKey )
            {
                LicenseLease lease = null;
                while ( true )
                {
                    DateTime day = VirtualDateTime.UtcNow.ToLocalTime();

                    // We don't start before 9.
                    DateTime startTime = day.Date.AddHours( 8 + (random.NextDouble()-0.5)*4 );
                    VirtualDateTime.WakeOn( startTime );

                    double probWorksToday;
                    switch (day.DayOfWeek)
                    {
                        case DayOfWeek.Sunday:
                            probWorksToday = user.WorksOnWeekend;
                            break;
                        case DayOfWeek.Saturday:
                            probWorksToday = user.WorksOnWeekend;
                            break;
                        default:
                            probWorksToday =1;
                            break;
                    }

                    if (random.NextDouble() < probWorksToday)
                    {
                        DateTime endTime = startTime.AddHours( 9 + random.NextDouble() );

                        // Which machine will he use this day?
                        int machineIndex = (int)Math.Floor(random.NextDouble() * user.Machines.Count);
                        string machine = user.Machines[machineIndex];

                       // We are running PostSharp every 15 minute.
                       for ( DateTime time = startTime; time < endTime; time = time.AddMinutes( 30*random.NextDouble() ) )
                        {
                       
                           //Console.WriteLine("{2} {0} waiting until {1}", VirtualDateTime.UtcNow.ToLocalTime(), time, user);
                            VirtualDateTime.WakeOn( time );

                            if ( lease == null || lease.RenewTime < time )
                            {
                                Interlocked.Increment( ref waitingUsers );
                                Console.WriteLine("{2} {0} on {3}: Acquiring lease; waiting users = {1}", user.AuthenticatedName, waitingUsers, VirtualDateTime.UtcNow.ToLocalTime(), machine);

                                string url = Program.Url.TrimEnd('/') + string.Format("/Lease.ashx?user={0}&machine={1}&product={2}",
                                                     user.AuthenticatedName, machine,
                                                     LicensedProduct.PostSharp30);

                                Stopwatch stopwatch = Stopwatch.StartNew();
                                lease = UserLicenseManager.GetLease(url, registryKey, VirtualDateTime.UtcNow.ToLocalTime(), messageSink);    

                                if ( lease == null )
                                {
                                    Console.WriteLine("Could not get a valid lease: lease is null");
                                }
                                else if ( lease.EndTime < time )
                                {
                                    Console.WriteLine("Could not get a valid lease: lease end time is in the past");
                                }
                                else if ( lease.RenewTime < time )
                                {
                                    Console.WriteLine("Got a lease with past renewal time: {0}, it is now {1}",
                                        lease.RenewTime, time);
                                    Program.FixVirtualTime();
                                }

                                Interlocked.Decrement(ref waitingUsers);
                                Console.WriteLine("{3} {0}: response received in {1}, waiting users = '{2}'", user, stopwatch.Elapsed, waitingUsers, VirtualDateTime.UtcNow.ToLocalTime());
                                
                            }
                        }

                    }

                }

            }

        }

    }
}