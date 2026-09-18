// Copyright (c) SharpCrafters s.r.o. See the LICENSE.md file in the root directory of this repository root for details.

namespace SharpCrafters.Backstage.LicenseServer.Services;

/// <summary>
/// The capacity arithmetic of a license, in one place so that the allocator and the health check
/// cannot disagree about how many seats a license covers.
/// </summary>
public static class LicenseCapacity
{
    /// <summary>
    /// Returns the number of seats a license covers while its grace period runs, which is its
    /// capacity raised by the percentage the license key carries and rounded up.
    /// </summary>
    public static int GetGraceLimit( int maximum, int gracePercent ) => (int) Math.Ceiling( maximum * ( 100.0 + gracePercent ) / 100.0 );
}