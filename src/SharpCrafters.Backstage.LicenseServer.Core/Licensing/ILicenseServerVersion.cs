// Copyright (c) SharpCrafters s.r.o. See the LICENSE.md file in the root directory of this repository root for details.

namespace SharpCrafters.Backstage.LicenseServer.Licensing;

/// <summary>
/// The version of the licensing library contained in this license server. The server cannot serve a
/// license key that requires a higher version until the server is upgraded.
/// </summary>
public interface ILicenseServerVersion
{
    Version LicensingLibraryVersion { get; }
}