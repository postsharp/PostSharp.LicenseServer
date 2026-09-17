using System.ComponentModel.DataAnnotations;

namespace SharpCrafters.Backstage.LicenseServer.Options;

/// <summary>
/// Settings of the license server. Replaces the <c>applicationSettings</c> section of the legacy
/// <c>Web.config</c>. Setting names are unchanged, so the existing administration documentation
/// remains valid.
/// </summary>
public sealed class LicenseServerOptions
{
    public const string SectionName = "LicenseServer";

    /// <summary>
    /// Gets or sets the number of days between notification emails about the license grace period,
    /// i.e. when a license has more leases than allowed.
    /// </summary>
    [Range( 0, 365 )]
    public int GracePeriodWarningDays { get; set; } = 1;

    /// <summary>
    /// Gets or sets the number of machines that one seat covers. A user working on more machines
    /// takes more than one seat: the number of machines divided by this value, rounded up. See
    /// <see cref="Data.SeatCounter"/>. The default value is 2. Check your license agreement for a
    /// different value.
    /// </summary>
    [Range( 1, 100 )]
    public int MachinesPerUser { get; set; } = 2;

    /// <summary>
    /// Gets or sets the minimal number of days before the end of the lease before a client will try
    /// to renew the lease. For instance, if developers are expected to work for five weeks without a
    /// network connection to the license server, this value should be greater than 35. Must be
    /// smaller than <see cref="NewLeaseDays"/>.
    /// </summary>
    [Range( 0, 3650 )]
    public int MinLeaseDays { get; set; } = 1;

    /// <summary>
    /// Gets or sets the duration of a new lease, in days.
    /// </summary>
    [Range( 1, 3650 )]
    public int NewLeaseDays { get; set; } = 3;

    /// <summary>
    /// Gets or sets the address for notification emails sent when the grace period starts.
    /// </summary>
    public string? GracePeriodWarningEmailTo { get; set; }

    /// <summary>
    /// Gets or sets the addresses copied on grace period notification emails.
    /// </summary>
    public string? GracePeriodWarningEmailCC { get; set; }

    /// <summary>
    /// Gets or sets the address for notification emails sent when a lease request is denied.
    /// </summary>
    public string? DeniedRequestEmailTo { get; set; }

    /// <summary>
    /// Gets or sets the timeout for the lock that serializes concurrent lease requests, in seconds.
    /// If a lease request cannot be served within this period, HTTP status 503 is returned.
    /// </summary>
    [Range( 1, 600 )]
    public int MutexTimeout { get; set; } = 30;

    /// <summary>
    /// Gets or sets the factor by which the passage of time is accelerated. Set to 1 in production.
    /// For testing purposes only.
    /// </summary>
    public decimal TimeAcceleration { get; set; } = 1;

    /// <summary>
    /// Gets or sets a semicolon-separated list of computer names of build servers. Build servers
    /// receive a lease that is not persisted, so that they do not consume developer seats.
    /// </summary>
    public string? BuildServers { get; set; }

    /// <summary>
    /// Gets or sets the base64-encoded key used to sign the lease audit log. When null, a key is
    /// generated on first start and persisted next to the application.
    /// </summary>
    public string? AuditHmacKey { get; set; }

    /// <summary>
    /// Gets or sets the Windows groups allowed to reach the administrative pages, for example
    /// <c>DOMAIN\PostSharp Administrators</c>. When empty, the administrative pages are not
    /// restricted, which preserves the behaviour of the legacy <c>Web.config</c>.
    /// </summary>
    public string[] AdminRoles { get; set; } = [];

    /// <summary>
    /// Gets or sets a value indicating whether a lease request must be authenticated. When false,
    /// anonymous requests are served, which preserves the behaviour of the legacy <c>Web.config</c>.
    /// </summary>
    public bool RequireAuthenticatedLeaseRequests { get; set; }

    /// <summary>
    /// Gets or sets the mechanism that serializes concurrent lease requests.
    /// </summary>
    public LeaseLockMode LeaseLockMode { get; set; } = LeaseLockMode.InProcess;

    /// <summary>
    /// Gets or sets the licensing authorities whose license keys this server accepts besides the
    /// production one. It exists so that a load simulation can be run against license keys that
    /// nobody sells, and the server refuses to start with a value here outside the Development
    /// environment.
    /// </summary>
    public TestLicensingAuthority[] TestLicensingAuthorities { get; set; } = [];

    /// <summary>
    /// Gets or sets a value indicating whether the server issues itself the license keys it serves.
    /// It exists so that a trial or a load simulation has something to lease without anybody buying
    /// a license first, and the server refuses to start with it set outside the Development
    /// environment.
    /// </summary>
    /// <remarks>
    /// The server generates a licensing authority of its own, keeps it in <see cref="DataDirectory"/>
    /// and trusts it. The license keys it signs are accepted by this server alone.
    /// </remarks>
    public bool SeedTestLicenses { get; set; }

    /// <summary>
    /// Gets or sets the directory holding the files the server generates and must not lose: the audit
    /// signing key, and the test licensing authority when there is one. Relative to the application
    /// by default. In a container it has to be a volume, or the audit signature chain restarts every
    /// time the container is replaced.
    /// </summary>
    public string DataDirectory { get; set; } = "App_Data";

    public TimeSpan MutexTimeoutSpan => TimeSpan.FromSeconds( this.MutexTimeout );
}
