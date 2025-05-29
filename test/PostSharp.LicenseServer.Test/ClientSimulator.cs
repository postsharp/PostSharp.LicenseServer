using Microsoft.Win32;
using PostSharp.Platform;
using PostSharp.Sdk;
using PostSharp.Sdk.Extensibility.Licensing;
using PostSharp.Sdk.Extensibility.Licensing.Helpers;
using System;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Threading;

namespace SharpCrafters.LicenseServer.Test
{
    class MemoryRegistryKey : IRegistryKey
    {
        public void Dispose()
        {
            throw new NotImplementedException();
        }

        public string[] GetSubKeyNames()
        {
            throw new NotImplementedException();
        }

        public IRegistryKey OpenSubKey(string subKey)
        {
            throw new NotImplementedException();
        }

        public IRegistryKey OpenSubKey(string name, bool writable)
        {
            throw new NotImplementedException();
        }

        public IRegistryKey CreateSubKey(string subKey)
        {
            throw new NotImplementedException();
        }

        public void DeleteSubKey(string subKey)
        {
            throw new NotImplementedException();
        }

        public void DeleteSubKey(string subKey, bool throwOnMissingSubKey)
        {
            throw new NotImplementedException();
        }

        public void DeleteSubKeyTree(string subKey)
        {
            throw new NotImplementedException();
        }

        public void DeleteSubKeyTree(string subKey, bool throwOnMissingSubKey)
        {
            throw new NotImplementedException();
        }

        public string[] GetValueNames()
        {
            throw new NotImplementedException();
        }

        public object GetValue(string name)
        {
            throw new NotImplementedException();
        }

        public object GetValue(string name, object defaultValue)
        {
            throw new NotImplementedException();
        }

        public void SetValue(string name, string value)
        {
            throw new NotImplementedException();
        }

        public void SetDWordValue(string name, int value)
        {
            throw new NotImplementedException();
        }

        public void SetQWordValue(string name, long value)
        {
            throw new NotImplementedException();
        }

        public void DeleteValue(string name)
        {
            throw new NotImplementedException();
        }

        public void DeleteValue(string name, bool throwOnMissingSubKey)
        {
            throw new NotImplementedException();
        }

        public void Close()
        {
            throw new NotImplementedException();
        }

        public bool CanMonitorChanges => throw new NotImplementedException();

        public SafeHandle Handle => throw new NotImplementedException();
    }
    
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

            IRegistryKey registryKey = new MemoryRegistryKey();

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
                                                     LicensedProduct.Ultimate);

                                Stopwatch stopwatch = Stopwatch.StartNew();
                                lease = LicenseServerClient.TryGetLease( url, registryKey, VirtualDateTime.UtcNow.ToLocalTime(), messageSink );

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