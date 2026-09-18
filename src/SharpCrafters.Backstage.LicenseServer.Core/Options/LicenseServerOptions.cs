// Copyright (c) SharpCrafters s.r.o. See the LICENSE.md file in the root directory of this repository root for details.

using System.ComponentModel.DataAnnotations;

namespace SharpCrafters.Backstage.LicenseServer.Options;

/// <summary>
/// Settings of the license server. Replaces the <c>applicationSettings</c> section of the legacy
/// <c>Web.config</c>. The names of the settings are unchanged, so the existing administration
/// documentation remains valid.
/// </summary>
public sealed class LicenseServerOptions
{
    public const string SectionName = "LicenseServer";

    /// <summary>
    /// Gets or sets the number of days between two notification e-mails about the grace period of a
    /// license, that is, the period during which a license has more leases than its capacity allows.
    /// </summary>
    [Range( 0, 365 )]
    public int GracePeriodWarningDays { get; set; } = 1;

    /// <summary>
    /// Gets or sets the number of machines that one seat covers. A user working on more machines
    /// takes more than one seat: the number of machines divided by this value, rounded up. See
    /// <see cref="Data.SeatCounter"/>. The default value is 2. Read your license agreement before you
    /// set another value.
    /// </summary>
    [Range( 1, 100 )]
    public int MachinesPerUser { get; set; } = 2;

    /// <summary>
    /// Gets or sets the number of days before the end of a lease at which a client renews that lease.
    /// When developers work five weeks without a network connection to the license server, set this
    /// value above 35. Must be smaller than <see cref="NewLeaseDays"/>.
    /// </summary>
    [Range( 0, 3650 )]
    public int MinLeaseDays { get; set; } = 1;

    /// <summary>
    /// Gets or sets the duration of a new lease, in days.
    /// </summary>
    [Range( 1, 3650 )]
    public int NewLeaseDays { get; set; } = 3;

    /// <summary>
    /// Gets or sets the address that receives a notification e-mail when the grace period starts.
    /// </summary>
    public string? GracePeriodWarningEmailTo { get; set; }

    /// <summary>
    /// Gets or sets the addresses copied on the notification e-mails about the grace period.
    /// </summary>
    public string? GracePeriodWarningEmailCC { get; set; }

    /// <summary>
    /// Gets or sets the address that receives a notification e-mail when a lease request is denied.
    /// </summary>
    public string? DeniedRequestEmailTo { get; set; }

    /// <summary>
    /// Gets or sets the timeout of the lock that serializes concurrent lease requests, in seconds.
    /// The server answers a lease request with the status 503 when it cannot obtain the lock within
    /// this period.
    /// </summary>
    [Range( 1, 600 )]
    public int MutexTimeout { get; set; } = 30;

    /// <summary>
    /// Gets or sets the factor by which the clock of the server runs faster than real time. Set it to
    /// 1 in production. It exists for tests.
    /// </summary>
    public decimal TimeAcceleration { get; set; } = 1;

    /// <summary>
    /// Gets or sets the names of the machines of the build servers, separated by semicolons. A build
    /// server receives a lease that is not stored, so that it does not consume the seat of a
    /// developer.
    /// </summary>
    public string? BuildServers { get; set; }

    /// <summary>
    /// Gets or sets the Windows groups allowed to open the administrative pages, for example
    /// <c>DOMAIN\PostSharp Administrators</c>. When the value is empty, the administrative pages are
    /// not restricted, which is the behaviour of the legacy <c>Web.config</c>.
    /// </summary>
    public string[] AdminRoles { get; set; } = [];

    /// <summary>
    /// Gets or sets a value indicating whether a lease request must be authenticated. When the value
    /// is false, the server serves anonymous requests, which is the behaviour of the legacy
    /// <c>Web.config</c>.
    /// </summary>
    public bool RequireAuthenticatedLeaseRequests { get; set; }

    /// <summary>
    /// Gets or sets the licensing authorities whose license keys this server accepts in addition to
    /// the production authority. This setting exists so that a load simulation can run against
    /// license keys that are not sold. The server refuses to start when this setting is used outside
    /// the Development environment.
    /// </summary>
    public TestLicensingAuthority[] TestLicensingAuthorities { get; set; } = [];

    /// <summary>
    /// Gets or sets a value indicating whether the server issues to itself the license keys it
    /// serves. This setting exists so that an evaluation or a load simulation has a license to lease
    /// before a license is bought. The server refuses to start when this setting is used outside the
    /// Development environment.
    /// </summary>
    /// <remarks>
    /// The server generates a licensing authority of its own, stores it in
    /// <see cref="DataDirectory"/>, and accepts its license keys. No other server accepts them.
    /// </remarks>
    public bool SeedTestLicenses { get; set; }

    /// <summary>
    /// Gets or sets the directory that contains the files the server generates, which is the test
    /// licensing authority when the server has one. A relative path is resolved against the
    /// application directory. In a container, this directory must be a volume. Otherwise the server
    /// generates a new test licensing authority every time the container is replaced, and the
    /// license keys of the previous authority stop being valid.
    /// </summary>
    public string DataDirectory { get; set; } = "App_Data";

    public TimeSpan MutexTimeoutSpan => TimeSpan.FromSeconds( this.MutexTimeout );
}