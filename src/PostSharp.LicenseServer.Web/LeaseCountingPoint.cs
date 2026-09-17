using System;

namespace PostSharp.LicenseServer
{
    public class LeaseCountingPoint
    {
        public DateTime Time;
        public LeaseCountingPointKind Kind;
        public Lease Lease;
        public int LeaseCount;
    }
}