#region Copyright (c) 2004-2010 by SharpCrafters s.r.o.

// This file is part of PostSharp source code and is the property of SharpCrafters s.r.o.
// 
// Source code is provided to customers under strict non-disclosure agreement (NDA). 
// YOU MUST HAVE READ THE NDA BEFORE HAVING RECEIVED ACCESS TO THIS SOURCE CODE. 
// Severe financial penalties apply in case of non respect of the NDA.

#endregion

using System.Collections.Generic;
using PostSharp.Platform.NetFramework;
using PostSharp.Platform.Neutral;
using PostSharp.Sdk.Extensibility.Licensing;
using ParsedLicense = PostSharp.Sdk.Extensibility.Licensing.License;

namespace PostSharp.LicenseServer
{
    public static class ParsedLicenseManager
    {
        private static readonly Dictionary<string, ParsedLicense> parsedLicenses = new Dictionary<string, ParsedLicense>();

        static ParsedLicenseManager()
        {
            CommonDefaultSystemServices.Initialize();
            // TODO: Add a missing NuGet package.
            NetFrameworkDefaultSystemServices.Initialize();
        }
            


        public static ParsedLicense GetParsedLicense( string licenseKey )
        {
            ParsedLicense parsedLicense;

            lock (parsedLicenses)
            {
                if (!parsedLicenses.TryGetValue(licenseKey, out parsedLicense))
                {
                    parsedLicense = ParsedLicense.Deserialize(licenseKey);

                    string errorDescription;
                    if (parsedLicense == null || !parsedLicense.Validate(null, out errorDescription))
                    {
                        return null;
                    }

                    parsedLicenses.Add(licenseKey, parsedLicense);
                }

                return parsedLicense;
            }
        }
    }
    
}