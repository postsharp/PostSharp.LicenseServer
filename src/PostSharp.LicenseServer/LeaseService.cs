#region Copyright (c) 2004-2010 by SharpCrafters s.r.o.

// This file is part of PostSharp source code and is the property of SharpCrafters s.r.o.
// 
// Source code is provided to customers under strict non-disclosure agreement (NDA). 
// YOU MUST HAVE READ THE NDA BEFORE HAVING RECEIVED ACCESS TO THIS SOURCE CODE. 
// Severe financial penalties apply in case of non respect of the NDA.

#endregion

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Net.Mail;
using System.Text.RegularExpressions;
using PostSharp.Sdk.Extensibility.Licensing;
using PostSharp.LicenseServer.Properties;
using PostSharp.Sdk;
using ParsedLicense = PostSharp.Sdk.Extensibility.Licensing.License;

namespace PostSharp.LicenseServer
{
    public class LeaseService
    {
        private readonly HashSet<string> buildServers = new HashSet<string>( StringComparer.OrdinalIgnoreCase );
        private readonly Regex computerUniqueIdRegex = new Regex( "-[0-9a-fA-F]+$", RegexOptions.Compiled );
        
        public LeaseService( bool sendEmails )
        {
            this.sendEmails = sendEmails;
            if ( !string.IsNullOrWhiteSpace( Settings.Default.BuildServers ) )
            {
                foreach (string buildServer in Settings.Default.BuildServers.Split( ';', ',', ' ' ))
                {
                    if ( !string.IsNullOrWhiteSpace( buildServer ) )
                    {
                        buildServers.Add( buildServer.Trim() );
                    }
                }
            }
        }

        private readonly bool sendEmails;

        internal static void CheckAssertions(Database db, License license, DateTime now)
        {
#if FALSE
            db.SubmitChanges();
            Dictionary<int, LicenseState> cache = new Dictionary<int, LicenseState>();
            List<string> errors = new List<string>();
            LicenseState licenseState = GetLicenseState( db, license, now, cache, errors );
            if ( licenseState == null )
                throw new Exception( string.Join( ". ", errors.ToArray() ) );

            
            if ( licenseState.Maximum.HasValue )
            {
                double max = Math.Ceiling(licenseState.Maximum.Value * (100.0 + Settings.Default.GracePeriodPercent) / 100.0);
                if (licenseState.Usage > max)
                    throw new Exception( "Maximum exceeded." );
            }
#endif
        }

        private static LicenseState GetLicenseState(Database db, License license, Version version, DateTime? buildDate, DateTime now, Dictionary<int, LicenseState> cache, Dictionary<int, string> errors)
        {
            LicenseState licenseState;

            if (cache.TryGetValue(license.LicenseId, out licenseState))
                return licenseState;

            ParsedLicense parsedLicense = ParsedLicenseManager.GetParsedLicense(license.LicenseKey);
            if (parsedLicense == null)
            {
                errors[license.LicenseId] = string.Format("The license key #{0} is invalid.", license.LicenseId);
                return null;
            }

            if (parsedLicense.MinPostSharpVersion > ApplicationInfo.Version)
            {
                errors[license.LicenseId] = string.Format(
                    "The license #{0} requires higher version of PostSharp on the License Server. Please upgrade PostSharp NuGet package of the License Server to >= {1}.{2}.{3}",
                    license.LicenseId,
                    parsedLicense.MinPostSharpVersion.Major,
                    parsedLicense.MinPostSharpVersion.Minor,
                    parsedLicense.MinPostSharpVersion.Build);
                return null;
            }

            if (parsedLicense.MinPostSharpVersion > version)
            {
                errors[license.LicenseId] = string.Format(
                    "The license #{0} of type {1} requires PostSharp version >= {2}.{3}.{4} but the requested version is {5}.{6}.{7}.",
                    license.LicenseId,
                    parsedLicense.LicenseType,
                    parsedLicense.MinPostSharpVersion.Major,
                    parsedLicense.MinPostSharpVersion.Minor,
                    parsedLicense.MinPostSharpVersion.Build,
                    version.Major,
                    version.Minor,
                    version.Build);
                return null;
            }

            if (!parsedLicense.IsLicenseServerEligible())
            {
                errors[license.LicenseId] = string.Format("The license #{0}, of type {1}, cannot be used in the license server.",
                    license.LicenseId, parsedLicense.LicenseType);
                return null;
            }


            if ( !(buildDate == null || parsedLicense.SubscriptionEndDate == null || buildDate <= parsedLicense.SubscriptionEndDate ) )
            {
                // PostSharp version number has been introduced in the license server protocol in PostSharp v5.
                if (version.Major >= 5)
                {
                    errors[license.LicenseId] = string.Format(
                        "The maintenance subscription of license #{0} ends on {1:d} but the requested version {2}.{3}.{4} has been built on {5:d}.",
                        license.LicenseId,
                        parsedLicense.SubscriptionEndDate,
                        version.Major,
                        version.Minor,
                        version.Build,
                        buildDate );
                }
                else
                {
                    errors[license.LicenseId] = string.Format(
                        "The maintenance subscription of license #{0} ends on {1:d} but the requested version has been built on {2:d}.",
                        license.LicenseId,
                        parsedLicense.SubscriptionEndDate,
                        buildDate );
                }

                return null;
            }

           
            licenseState = new LicenseState(now, db, license, parsedLicense);
            cache.Add(license.LicenseId, licenseState);
            return licenseState;
        }

        public LicenseLease GetLicenseLease(Database db, string productCode, Version version, DateTime? buildDate, string machine, string userName, string authenticatedUserName, DateTime now, Dictionary<int, string> errors)
        {
            Settings settings = Settings.Default;

            // Get suitable active licenses.
            License[] licenses = (from license in db.Licenses
                                  where (string.IsNullOrEmpty(productCode) || license.ProductCode == productCode) && license.Priority >= 0
                                  orderby license.Priority
                                  select license).ToArray();

            // Check if the machine belongs to build server user.
            if ( IsBuildServer( machine ) )
            {
                License buildServerLicense = GetBuildServerLicense( licenses );
                if ( buildServerLicense != null )
                {
                    DateTime endTime = now.AddDays( Settings.Default.NewLeaseDays );
                    LicenseLease buildServerLicenseLease = new LicenseLease( buildServerLicense.LicenseKey, now, endTime, endTime.AddDays( -settings.MinLeaseDays ) );

                    // Lease for build server should not be stored - build server users shouldn't steal developer licenses.
                    return buildServerLicenseLease;
                }

                // try to acquire lease in normal way
            }

            Lease lease = GetLease(db, productCode, version, buildDate, machine, userName, authenticatedUserName, now, errors, licenses);
            if ( lease == null )
                return null;

            LicenseLease licenseLease = new LicenseLease(lease.License.LicenseKey,
                    lease.StartTime, lease.EndTime, lease.EndTime.AddDays(-settings.MinLeaseDays));

           return licenseLease;
        }

        public Lease GetLease(Database db, string productCode, Version version, DateTime? buildDate, string machine, string userName, string authenticatedUserName, DateTime now, Dictionary<int, string> errors)
        {
            License[] licenses = (from license in db.Licenses
                                  where license.ProductCode == productCode
                                  orderby license.Priority, license.LicenseId descending
                                  select license).ToArray();

            return this.GetLease( db, productCode, version, buildDate, machine, userName, authenticatedUserName, now, errors, licenses );
        }

        public Lease GetLease( Database db, string productCode, Version version, DateTime? buildDate, string machine, string userName, string authenticatedUserName, DateTime now, Dictionary<int, string> errors, License[] licenses )
        {
            Dictionary<int, LicenseState> licenseStates = new Dictionary<int, LicenseState>();
        
            Settings settings = Settings.Default;

            // Check if we have a lease that we can use.
            foreach (License license in licenses)
            {
                LicenseState licenseState = GetLicenseState(db, license, version, buildDate, now, licenseStates, errors);
                if ( licenseState == null ) continue;

               
         
                int licenseId = license.LicenseId;
                Lease[] currentLeases = (from l in db.OpenLeases
                                         where
                                             l.LicenseId == licenseId &&
                                             l.StartTime <= now &&
                                             l.EndTime > now &&
                                             l.UserName == userName
                                         orderby l.StartTime
                                         select l).ToArray();

                Dictionary<string, string> machines = new Dictionary<string, string>( StringComparer.OrdinalIgnoreCase );
                foreach (Lease candidateLease in currentLeases)
                {
                    machines[candidateLease.Machine] = candidateLease.Machine;

                    if (candidateLease.Machine != machine)
                        continue;

                    if (candidateLease.EndTime > now.AddDays(settings.MinLeaseDays))
                    {
                        // We have already a good lease.
                        return candidateLease;
                    }
                    else
                    {
                        // Invariant: we can always prolong a lease because licenses are always acquired from
                        // the present moment, therefore taking the current lease into account.
                        // UNLESS it's the end of the license period or the grace period.
                        Lease lease = db.ProlongLease( candidateLease, authenticatedUserName, now );
                        if ( lease == null )
                        {
                            continue;
                        }
                        return lease;
                    }
                }

                // We did not find a lease for the requested machines.
                // See if we can do a new lease that would not increment the number of concurrent users.
                if (machines.Count % Settings.Default.MachinesPerUser != 0)
                {
                    CheckAssertions( db, license, now );
                    Lease lease = db.CreateLease(license, userName, machine, authenticatedUserName, now, licenseState.InExcess);
                    if (lease != null) return lease;
                }
            }

            // We don't have a current lease. Create a new one.
            foreach (License license in licenses)
            {
                LicenseState licenseState = GetLicenseState(db, license, version, buildDate, now, licenseStates, errors);
                if (licenseState == null) continue;


                if (licenseState.Maximum.HasValue && licenseState.Maximum.Value <= licenseState.Usage 
                    && ( !licenseState.ParsedLicense.ValidTo.HasValue || now < licenseState.ParsedLicense.ValidTo ))
                {
                    // Not enough users for this license.
                    continue;
                }
                
               
                Lease lease = db.CreateLease( license, userName, machine, authenticatedUserName, now, false );
                if (lease != null) return lease;
            }

            // We did not find a suitable license. See if we can use the grace period.
            foreach (License license in licenses)
            {
                LicenseState licenseState = GetLicenseState(db, license, version, buildDate, now, licenseStates, errors);
              
                if ( licenseState == null ) continue;


                if (license.GraceStartTime == null)
                {
                    license.GraceStartTime = now;
                }

                int graceLimit = (int)Math.Ceiling(licenseState.Maximum.Value * (100.0 + licenseState.ParsedLicense.GetGracePercentOrDefault()) / 100.0);
                DateTime graceEnd = license.GraceStartTime.Value.AddDays(licenseState.ParsedLicense.GetGraceDaysOrDefault());

                if (license.GraceStartTime <= now && graceEnd > now && licenseState.Usage  < graceLimit)
                {
                    // We will use the grace period.

                    // See if we should send a warning message.
                    if (license.GraceLastWarningTime.GetValueOrDefault(DateTime.MinValue).AddDays(settings.GracePeriodWarningDays) < now)
                    {
                        try
                        {
                            string body = string.Format(
                                "The license #{0} has a capacity of {1} concurrent user(s), but {2} users are currently using the product {3}. " +
                                "The grace period has started on {4} and will end on {5}. After this date, additional leases will be denied." +
                                "Please contact PostSharp Technologies to acquire additional licenses.",
                                license.LicenseId, licenseState.Maximum, licenseState.Usage + 1, licenseState.ParsedLicense.Product,
                                license.GraceStartTime, graceEnd);
                            const string subject = "WARNING: licensing capacity exceeded";

                            SendEmail(settings.GracePeriodWarningEmailTo, settings.GracePeriodWarningEmailCC, subject, body);
                            license.GraceLastWarningTime = now;
                        }
                        catch (Exception e)
                        {
                            Trace.TraceError(e.ToString());
                        }
                    }

                    Lease lease = db.CreateLease(license, userName, machine, authenticatedUserName, now, true);
                    if (lease != null) return lease;
                }
            }


            SendEmail(settings.DeniedRequestEmailTo, null, "ERROR: license request denied",
                       string.Format("No license with free capacity was found to satisfy the lease request for the product {0} from " +
                                      "the user '{1}' (authentication: '{2}'), machine '{3}'. " + string.Join(". ", errors.ToArray()),
                                      productCode, userName, authenticatedUserName, machine));

            return null;
        }

        private License GetBuildServerLicense(License[] licenses)
        {
            return licenses.FirstOrDefault( IsLicenseValid );
        }

        private static bool IsLicenseValid( License license )
        {
            return ParsedLicenseManager.GetParsedLicense( license.LicenseKey ) != null;
        }

        private bool IsBuildServer( string machineName )
        {
            machineName = computerUniqueIdRegex.Replace( machineName, string.Empty );

            return buildServers.Contains( machineName );
        }

        private void SendEmail(string to, string cc, string subject, string body)
        {
            if (!this.sendEmails) return;
            if (string.IsNullOrEmpty(to) || to.Trim().Length == 0) return;

            to = to.Trim( ' ', '\n', '\r', '\t' );
            

            MailMessage message = new MailMessage("sales@postsharp.net", to,
                                                   "[SharpCrafters License Server] " + subject, body)
            {
                Priority = MailPriority.High
            };

            if (!string.IsNullOrEmpty(cc))
            {
                foreach (string address in cc.Split(',', ';', ' '))
                {
                    message.CC.Add(address.Trim( ' ', '\n', '\r', '\t' ));
                }
            }

            SmtpClient smtp = new SmtpClient();
            smtp.SendAsync( message, null );
        }

        private class LicenseState
        {
            private readonly DateTime time;
            private readonly Database db;
            private readonly License license;
            private int usage = -1;

            public LicenseState( DateTime time, Database db, License license, ParsedLicense parsedLicense )
            {
                this.time = time;
                this.db = db;
                this.license = license;
                this.ParsedLicense = parsedLicense;
            }

            private void ComputeUsage()
            {
                if ( this.usage == -1 )
                {
                     this.usage = db.GetActiveLeads(license.LicenseId, time);
                }
            }
            
            public int Usage
            {
                get
                {
                    this.ComputeUsage();
                    return usage;
                }
            }
                
            public int? Maximum
            {
                get { return this.ParsedLicense.UserNumber; }
            }

            public bool InExcess
            {
                get { return this.Maximum.HasValue && this.Maximum.Value < this.Usage; }
            }

            public ParsedLicense ParsedLicense { get; private set; }

        }
    }
}