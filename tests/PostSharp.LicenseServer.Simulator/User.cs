using System.Collections.Generic;

namespace SharpCrafters.LicenseServer.Test
{
    class User
    {
        public string UserName;
        public string AuthenticatedName;
        public double WorksOnWeekend;
        public readonly List<string> Machines = new List<string>();

        public override string ToString()
        {
            return this.UserName;
        }
    }
}