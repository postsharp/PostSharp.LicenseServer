using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;
using PostSharp.LicenseServer.Properties;

namespace PostSharp.LicenseServer
{
    public static class VirtualDateTime
    {
        private static DateTime startTime = DateTime.UtcNow;

        public static readonly decimal Acceleration = Settings.Default.TimeAcceleration;

        public static DateTime UtcNow
        {
            get
            {
#if DEBUG
                if ( Acceleration != 0 && Acceleration != 1 )
                {
                    return startTime.AddMilliseconds( (DateTime.UtcNow - startTime).TotalMilliseconds* (double) Acceleration );
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
    }
}