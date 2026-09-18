// Copyright (c) SharpCrafters s.r.o. See the LICENSE.md file in the root directory of this repository root for details.

using SharpCrafters.Backstage.LicenseServer.Licensing;

namespace SharpCrafters.Backstage.LicenseServer.Tests.Infrastructure;

/// <summary>
/// Fixed points in time used by the tests.
/// </summary>
public static class TestClock
{
    /// <summary>
    /// A Monday, at a whole second.
    /// </summary>
    /// <remarks>
    /// Monday exercises the branch of the usage graph that labels the weekdays. Whole seconds keep
    /// the tests independent of the rounding of the SQL type <c>datetime</c>, which is 1/300 of a
    /// second.
    /// </remarks>
    public static readonly DateTime Origin = new( 2026, 1, 5, 9, 0, 0, DateTimeKind.Utc );

    public static DateTime Days( double days ) => Origin.AddDays( days );

    public static DateTime Hours( double hours ) => Origin.AddHours( hours );
}