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
using System.Threading;
using System.Web;
using PostSharp.Sdk.Extensibility.Licensing;
using PostSharp.LicenseServer.Properties;
using ParsedLicense = PostSharp.Sdk.Extensibility.Licensing.License;

namespace PostSharp.LicenseServer
{
    /// <summary>
    /// Summary description for Lease1
    /// </summary>
    public sealed class LeaseHandler : IHttpHandler
    {
        private readonly List<string> errors = new List<string>();
        private HttpContext context;
        static Mutex mutex = CreateMutex();

        static LeaseHandler()
        {
            AppDomain.CurrentDomain.DomainUnload += ( sender, args ) => mutex.Close();
        }

        private static Mutex CreateMutex()
        {
            return new Mutex(false, "PostSharp.LicenseServer.Lease");
        }

        public void ProcessRequest( HttpContext context )
        {
            this.context = context;
            using ( Database db = new Database() )
            {

            Settings settings = Settings.Default;

            string productCode = context.Request.QueryString["product"];
            if ( string.IsNullOrEmpty( productCode ) )
            {
                this.SetError( 400, "Missing query string argument: product." );
                return;
            }

            string machine = context.Request.QueryString["machine"];

            if ( string.IsNullOrEmpty( machine ) )
            {
                this.SetError( 400, "Missing query string argument: machine." );
                return;
            }

            string userName = context.Request.QueryString["user"];

            if ( string.IsNullOrEmpty( userName ) )
            {
                this.SetError( 400, "Missing query string argument: user." );
                return;
            }

            machine = machine.ToLowerInvariant();
            userName = userName.ToLowerInvariant();

                bool releaseMutex = false;
            try
            {

                    while ( true )
                    {
                        try
                        {

                            if ( !mutex.WaitOne( TimeSpan.FromSeconds( Settings.Default.MutexTimeout ) ) )
                            {
                                this.SetError( 503, "Service overloaded." );
                                return;
                            }
                            else
                            {
                                releaseMutex = true;
                                break;
                            }

                        }
                        catch ( AbandonedMutexException )
                        {
                            try
                            {
                                mutex.ReleaseMutex();
                            }
                            catch
                            {
                            }

                            try
                            {
                                mutex.Close();
                            }
                            catch
                            {
                            }

                            mutex = CreateMutex();
                        }
                    }

                    Stopwatch stopwatch = Stopwatch.StartNew();

                    LicenseLease licenseLease = new LeaseService(true).GetLicenseLease(db, productCode, machine, userName, context.User.Identity.Name, VirtualDateTime.UtcNow,
                                                                     errors );

                    if ( licenseLease != null )
                    {
                        db.SubmitChanges();

                        string serializedLease = licenseLease.Serialize();
                        context.Response.Write( serializedLease );
                    }
                    else
                    {
                        this.SetError( 403, "No license with free capacity. " + string.Join( " ", this.errors.ToArray() ) );
                    }

                    if ( stopwatch.ElapsedMilliseconds > 1000 )
                    {
                        context.Trace.Warn( string.Format( "Request served in {0}", stopwatch.Elapsed ) );
                    }
                }
            finally
            {
                    if ( releaseMutex )
                    {
                        try
                        {
                mutex.ReleaseMutex();
            }
                        catch
                        {

        }
                    }
                }
            }
        }

        private void SetError( int statusCode, string description )
        {
            this.context.Response.StatusCode = statusCode;
            this.context.Response.Write( description );
            this.context.Response.End();
        }


        public bool IsReusable
        {
            get { return false; }
        }
    }
}