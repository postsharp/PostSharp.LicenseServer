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
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using PostSharp.LicenseServer.Properties;

namespace PostSharp.LicenseServer
{
    public partial class Database
    {
        public string GetSignature(Lease lease)
        {
            Lease lastLease = (from l in this.Leases orderby l.LeaseId descending select l).FirstOrDefault();

            StringWriter stringWriter = new StringWriter();
            stringWriter.Write(lastLease != null ? lastLease.HMAC : "");
            stringWriter.Write(';');
            lease.Write(stringWriter, false);


            using (HMAC hmac = HMAC.Create())
            {
                return Convert.ToBase64String(hmac.ComputeHash(Encoding.UTF8.GetBytes(stringWriter.ToString())));
            }
        }

        public Lease CreateLease(License license, string user, string machine, string authenticatedUserName, DateTime time, bool grace)
        {
            Lease lease = new Lease
                              {
                                  License = license,
                                  AuthenticatedUser = authenticatedUserName,
                                  EndTime = time.AddDays(Settings.Default.NewLeaseDays),
                                  Machine = machine,
                                  StartTime = time,
                                  UserName = user,
                                  Grace = grace
                              };

            if (!this.FixLease(lease, time))
                return null;

            
            this.Leases.InsertOnSubmit((lease));

            LeaseService.CheckAssertions(this, license, time);


            return lease;
        }

        public Lease ProlongLease(Lease oldLease, string authenticatedUserName, DateTime time)
        {
            Lease newLease = new Lease
                                 {
                                     License = oldLease.License,
                                     AuthenticatedUser = authenticatedUserName,
                                     OverwritesLease = oldLease,
                                     StartTime = oldLease.StartTime,
                                     EndTime = time.AddDays(Settings.Default.NewLeaseDays),
                                     Machine = oldLease.Machine,
                                     UserName = oldLease.UserName,
                                     Grace = oldLease.Grace
                                 };

            if (!this.FixLease(newLease, time))
                return null;

            this.Leases.InsertOnSubmit(newLease);

            LeaseService.CheckAssertions( this, newLease.License, time );

            return newLease;
        }

     
        public void CancelLease(Lease lease, string authenticatedUserName, DateTime time)
        {
            Lease overwrite = new Lease
            {
                AuthenticatedUser = authenticatedUserName,
                License = lease.License,
                UserName = lease.UserName,
                Machine = lease.Machine,
                StartTime = lease.StartTime,
                OverwritesLease = lease,
                Grace = lease.Grace,
                EndTime = time,
            };
            this.FixLease( overwrite, time, false );
            this.Leases.InsertOnSubmit( overwrite );
        }

        private bool FixLease(Lease lease, DateTime time, bool fixEndTime = true)
        {
            PostSharp.Sdk.Extensibility.Licensing.License parsedLicense = ParsedLicenseManager.GetParsedLicense(lease.License.LicenseKey);

            if (lease.EndTime <= lease.StartTime)
                throw new Exception( "Assertion failed." );
          
            if (fixEndTime)
            {
                if ( parsedLicense.ValidTo.HasValue && parsedLicense.ValidTo < lease.EndTime )
                {
                    lease.EndTime = parsedLicense.ValidTo.Value;
                }

                if ( lease.Grace )
                {
                    DateTime graceEnd = lease.License.GraceStartTime.Value.AddDays( parsedLicense.GetGraceDaysOrDefault() );
                    if ( lease.EndTime > graceEnd )
                    {
                        lease.EndTime = graceEnd;
                    }
                }


                if ( lease.EndTime <= time )
                    return false;

                if (lease.EndTime <= lease.StartTime)
                    throw new Exception("Assertion failed.");
            }

            lease.HMAC = this.GetSignature(lease);
          

            return true;
        }

        public IQueryable<Lease> OpenLeases
        {
            get
            {
                return from l in this.Leases
                       join Lease o in this.Leases on l.LeaseId equals o.OverwrittenLeaseId into o
                       from oo in o.DefaultIfEmpty()
                       where oo == null
                       select l;
            }
        }

        public IEnumerable<LeaseCountingPoint> GetLeaseCountingPoints(int licenseId, DateTime startTime, DateTime endTime)
        {
            IQueryable<LeaseCountingPoint> openingLeases = from l in this.OpenLeases
                                                           where l.LicenseId == licenseId &&
                                                                 l.StartTime <= endTime &&
                                                                 l.EndTime > startTime
                                                           select new LeaseCountingPoint {Time = l.StartTime, Kind = LeaseCountingPointKind.Open, Lease = l};

            IQueryable<LeaseCountingPoint> closingLeases = from l in this.OpenLeases
                                                         where l.LicenseId == licenseId &&
                                                               l.StartTime <= endTime &&
                                                               l.EndTime > startTime
                                                           select new LeaseCountingPoint { Time = l.EndTime, Kind = LeaseCountingPointKind.Close, Lease = l };


            IQueryable<LeaseCountingPoint> allRecords = from l in openingLeases.Concat(closingLeases) orderby l.Time, l.Kind select l;

            Dictionary<string, List<string>> currentUsers = new Dictionary<string, List<string>>();

            int leaseCount = 0;
            foreach (LeaseCountingPoint record in allRecords)
            {
                List<string> machines;
                if (!currentUsers.TryGetValue(record.Lease.UserName, out machines))
                {
                    machines = new List<string>();
                    currentUsers.Add(record.Lease.UserName, machines);
                }

                int licensesBefore = (int)Math.Ceiling(machines.Count / (float)Settings.Default.MachinesPerUser);

                if (record.Kind == LeaseCountingPointKind.Open)
                {
                    if ( !machines.Contains( record.Lease.Machine, StringComparer.OrdinalIgnoreCase ) )
                    {
                        machines.Add(record.Lease.Machine);
                    }
                }
                else if ( record.Kind == LeaseCountingPointKind.Close )
                {
                    string machine = record.Lease.Machine;
                    int index = machines.FindIndex( s => string.Equals( s, machine, StringComparison.OrdinalIgnoreCase ) );
                    
                    if ( index >= 0 )
                    {
                        machines.RemoveAt( index );
                    }
                    else
                    {
                        throw new Exception("Assertion failed.");
                    }
                }

                int licensesAfter = (int)Math.Ceiling(machines.Count / (float)Settings.Default.MachinesPerUser);

                leaseCount += licensesAfter - licensesBefore;
                record.LeaseCount = leaseCount;

                yield return record;
            }
        }

        public int GetActiveLeads(int licenseId, DateTime dateTime)
        {
            IQueryable<int> machinesPerUser = from l in this.OpenLeases
                    where l.StartTime <= dateTime && l.EndTime > dateTime && l.LicenseId == licenseId
                    group l by l.UserName
                    into u
                    select u.Count();

            IQueryable<double> query = from u in machinesPerUser
                                       group u by 0
                                       into g select g.Sum( i => Math.Ceiling( i/(double) Settings.Default.MachinesPerUser ) );

            return (int) query.SingleOrDefault();
        }
    }
}