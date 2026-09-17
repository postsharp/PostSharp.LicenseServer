#region Copyright (c) 2004-2010 by SharpCrafters s.r.o.

// This file is part of PostSharp source code and is the property of SharpCrafters s.r.o.
// 
// Source code is provided to customers under strict non-disclosure agreement (NDA). 
// YOU MUST HAVE READ THE NDA BEFORE HAVING RECEIVED ACCESS TO THIS SOURCE CODE. 
// Severe financial penalties apply in case of non respect of the NDA.

#endregion

using System.Collections.Specialized;

namespace PostSharp.LicenseServer
{
    internal static class ExtensionMethods
    {
        public static int GetInt32( this NameValueCollection collection, string name, int defaultValue )
        {
            string value = collection[name];
            int intValue;
            if ( string.IsNullOrEmpty( value ) || !int.TryParse( value, out intValue ) ) return defaultValue;

            return intValue;
        }
    }
}