using System;
using System.Threading;

namespace SharpCrafters.LicenseServer.Test
{
    public static class VirtualDateTime
    {
        private static DateTime initTimeUtc = DateTime.UtcNow;
        private static DateTime startTimeUtc = DateTime.UtcNow;
        private static decimal acceleration = 0;

        public static void Initialize( DateTime time, decimal acceleration )
        {
            Console.WriteLine("Setting time from {0} to {1}, acceleration={2}", startTimeUtc, time, acceleration);
            VirtualDateTime.initTimeUtc = DateTime.UtcNow;
            VirtualDateTime.startTimeUtc = time;
            VirtualDateTime.acceleration = acceleration;
        }

        public static DateTime UtcNow
        {
            get
            {
#if DEBUG
                if (acceleration != 0 && acceleration != 1)
                {
                    return startTimeUtc.AddMilliseconds((DateTime.UtcNow - initTimeUtc).TotalMilliseconds * (double)acceleration);
                }
                else
                {
                    return DateTime.UtcNow;
                }
#else
                return DateTime.UtcNow;
#endif
            }
        }

        public static void WakeOn( DateTime time )
        {
            DateTime realTime = initTimeUtc.AddMilliseconds( ((time.ToUniversalTime() - startTimeUtc).TotalMilliseconds/(double) acceleration) );
            TimeSpan timeSpan = realTime - DateTime.UtcNow;
            if ( timeSpan.TotalMilliseconds > 0 )
            {
                Thread.Sleep( (int) timeSpan.TotalMilliseconds );
            }
        }

    }
}