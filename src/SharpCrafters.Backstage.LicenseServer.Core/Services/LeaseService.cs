using System.Diagnostics.CodeAnalysis;
using System.Text.RegularExpressions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using SharpCrafters.Backstage.LicenseServer.Data;
using SharpCrafters.Backstage.LicenseServer.Email;
using SharpCrafters.Backstage.LicenseServer.Licensing;
using SharpCrafters.Backstage.LicenseServer.Options;

namespace SharpCrafters.Backstage.LicenseServer.Services;

/// <summary>
/// Decides which license, if any, satisfies a lease request.
/// </summary>
/// <remarks>
/// Allocation runs in three passes over the candidate licenses: reuse or prolong a lease the user
/// already holds, then grant a new lease against spare capacity, then fall back on the grace period.
/// </remarks>
public sealed partial class LeaseService
{
    private readonly ILeaseRepository repository;
    private readonly LicenseServerOptions settings;
    private readonly ILicenseParser licenseParser;
    private readonly ILicenseServerVersion serverVersion;
    private readonly IEmailSender emailSender;
    private readonly ILogger<LeaseService> logger;
    private readonly HashSet<string> buildServers = new( StringComparer.OrdinalIgnoreCase );

    public LeaseService(
        ILeaseRepository repository,
        IOptions<LicenseServerOptions> options,
        ILicenseParser licenseParser,
        ILicenseServerVersion serverVersion,
        IEmailSender emailSender,
        ILogger<LeaseService> logger )
    {
        this.repository = repository;
        this.settings = options.Value;
        this.licenseParser = licenseParser;
        this.serverVersion = serverVersion;
        this.emailSender = emailSender;
        this.logger = logger;

        if ( !string.IsNullOrWhiteSpace( this.settings.BuildServers ) )
        {
            foreach ( string buildServer in this.settings.BuildServers.Split( [';', ',', ' '] ) )
            {
                if ( !string.IsNullOrWhiteSpace( buildServer ) )
                {
                    this.buildServers.Add( buildServer.Trim() );
                }
            }
        }
    }

    /// <summary>
    /// Matches the unique-identifier suffix that build agents append to their machine name.
    /// </summary>
    [GeneratedRegex( "-[0-9a-fA-F]+$" )]
    private static partial Regex ComputerUniqueIdRegex { get; }

    /// <summary>
    /// Validates a license against the request and, if it is usable, returns its current state.
    /// </summary>
    /// <returns><c>null</c> when the license cannot serve this request, in which case
    /// <paramref name="errors"/> explains why.</returns>
    private LicenseState? GetLicenseState(
        License license,
        Version version,
        DateTime? buildDate,
        DateTime now,
        Dictionary<int, LicenseState> cache,
        Dictionary<int, string> errors )
    {
        if ( cache.TryGetValue( license.LicenseId, out LicenseState? licenseState ) )
        {
            return licenseState;
        }

        LicenseInfo? parsedLicense = this.licenseParser.TryParse( license.LicenseKey );

        if ( parsedLicense == null )
        {
            errors[license.LicenseId] = $"The license key #{license.LicenseId} is invalid.";

            return null;
        }

        if ( parsedLicense.MinPostSharpVersion > this.serverVersion.LicensingLibraryVersion )
        {
            errors[license.LicenseId] = string.Format(
                "The license #{0} requires a higher version of the licensing library on the License Server. Please upgrade the License Server to >= {1}.{2}.{3}",
                license.LicenseId,
                parsedLicense.MinPostSharpVersion.Major,
                parsedLicense.MinPostSharpVersion.Minor,
                parsedLicense.MinPostSharpVersion.Build );

            return null;
        }

        if ( parsedLicense.MinPostSharpVersion > version )
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
                version.Build );

            return null;
        }

        if ( !parsedLicense.IsLicenseServerEligible )
        {
            errors[license.LicenseId] =
                $"The license #{license.LicenseId}, of type {parsedLicense.LicenseType}, cannot be used in the license server.";

            return null;
        }

        if ( !(buildDate == null || parsedLicense.SubscriptionEndDate == null
                                 || buildDate <= parsedLicense.SubscriptionEndDate) )
        {
            // The PostSharp version number was introduced in the license server protocol in v5.
            errors[license.LicenseId] = version.Major >= 5
                ? string.Format(
                    "The maintenance subscription of license #{0} ends on {1:d} but the requested version {2}.{3}.{4} has been built on {5:d}.",
                    license.LicenseId,
                    parsedLicense.SubscriptionEndDate,
                    version.Major,
                    version.Minor,
                    version.Build,
                    buildDate )
                : string.Format(
                    "The maintenance subscription of license #{0} ends on {1:d} but the requested version has been built on {2:d}.",
                    license.LicenseId,
                    parsedLicense.SubscriptionEndDate,
                    buildDate );

            return null;
        }

        licenseState = new LicenseState( now, this.repository, license, parsedLicense );
        cache.Add( license.LicenseId, licenseState );

        return licenseState;
    }

    /// <summary>
    /// Serves a lease request, returning the license key and the validity of the lease.
    /// </summary>
    public async Task<GrantedLease?> GetLicenseLeaseAsync(
        string? productCode,
        Version version,
        DateTime? buildDate,
        string machine,
        string userName,
        string authenticatedUserName,
        DateTime now,
        Dictionary<int, string> errors,
        CancellationToken cancellationToken = default )
    {
        // A client that names no product is served from any pool, which is what every PostSharp
        // client relies on: none of them sent the argument.
        IReadOnlyList<string> productCodes = string.IsNullOrEmpty( productCode )
            ? []
            : ProductCodes.Matching( productCode );

        License[] licenses = await this.repository.Licenses
            .Where( license => (productCodes.Count == 0 || productCodes.Contains( license.ProductCode ))
                               && license.Priority >= 0 )
            .OrderBy( license => license.Priority )
            .ToArrayAsync( cancellationToken );

        if ( this.IsBuildServer( machine ) )
        {
            Dictionary<int, LicenseState> buildServerStates = [];

            // A build agent is exempt from consuming a seat, not from the rules about which licenses
            // may be served at all, so the same validation runs as for anybody else.
            foreach ( License candidate in licenses )
            {
                LicenseState? state =
                    this.GetLicenseState( candidate, version, buildDate, now, buildServerStates, errors );

                if ( state == null )
                {
                    continue;
                }

                DateTime endTime = now.AddDays( this.settings.NewLeaseDays );

                if ( state.ParsedLicense.ValidTo.HasValue && state.ParsedLicense.ValidTo < endTime )
                {
                    endTime = state.ParsedLicense.ValidTo.Value;
                }

                if ( endTime <= now )
                {
                    // The license expires before the lease would begin.
                    continue;
                }

                // A build server's lease is never persisted, so that build agents cannot consume
                // the seats of the developers they build for.
                return new GrantedLease(
                    candidate.LicenseKey,
                    now,
                    endTime,
                    endTime.AddDays( -this.settings.MinLeaseDays ) );
            }

            // Otherwise fall through and acquire a lease in the normal way.
        }

        Lease? lease = await this.GetLeaseAsync(
            version,
            buildDate,
            machine,
            userName,
            authenticatedUserName,
            now,
            errors,
            licenses,
            productCode,
            cancellationToken );

        if ( lease == null )
        {
            return null;
        }

        return new GrantedLease(
            lease.License.LicenseKey,
            lease.StartTime,
            lease.EndTime,
            lease.EndTime.AddDays( -this.settings.MinLeaseDays ) );
    }

    /// <summary>
    /// Finds or creates the lease that satisfies a request.
    /// </summary>
    public async Task<Lease?> GetLeaseAsync(
        Version version,
        DateTime? buildDate,
        string machine,
        string userName,
        string authenticatedUserName,
        DateTime now,
        Dictionary<int, string> errors,
        License[] licenses,
        string? productCode = null,
        CancellationToken cancellationToken = default )
    {
        Dictionary<int, LicenseState> licenseStates = [];

        // First pass: a lease this user already holds on this machine, reused or prolonged.
        foreach ( License license in licenses )
        {
            LicenseState? licenseState =
                this.GetLicenseState( license, version, buildDate, now, licenseStates, errors );

            if ( licenseState == null )
            {
                continue;
            }

            int licenseId = license.LicenseId;

            Lease[] currentLeases = await this.repository.OpenLeases
                .Where( l => l.LicenseId == licenseId && l.StartTime <= now && l.EndTime > now && l.UserName == userName )
                .Include( l => l.License )
                .OrderBy( l => l.StartTime )
                .ToArrayAsync( cancellationToken );

            Dictionary<string, string> machines = new( StringComparer.OrdinalIgnoreCase );

            foreach ( Lease candidateLease in currentLeases )
            {
                machines[candidateLease.Machine] = candidateLease.Machine;

                if ( candidateLease.Machine != machine )
                {
                    continue;
                }

                if ( candidateLease.EndTime > now.AddDays( this.settings.MinLeaseDays ) )
                {
                    // The lease the user already holds is good enough.
                    return candidateLease;
                }

                // A lease can always be prolonged, because leases are acquired from the present
                // moment and therefore already account for the current one -- unless the license
                // period or the grace period ends first.
                Lease? prolonged = this.repository.ProlongLease( candidateLease, authenticatedUserName, now );

                if ( prolonged == null )
                {
                    continue;
                }

                return prolonged;
            }

            // No lease for the requested machine. A further machine for a user who already holds a
            // seat is free, up to MachinesPerUser.
            if ( machines.Count % this.settings.MachinesPerUser != 0 )
            {
                Lease? lease = this.repository.CreateLease(
                    license,
                    userName,
                    machine,
                    authenticatedUserName,
                    now,
                    licenseState.InExcess );

                if ( lease != null )
                {
                    return lease;
                }
            }
        }

        // Second pass: a new lease against spare capacity.
        foreach ( License license in licenses )
        {
            LicenseState? licenseState =
                this.GetLicenseState( license, version, buildDate, now, licenseStates, errors );

            if ( licenseState == null )
            {
                continue;
            }

            if ( licenseState.Maximum.HasValue && licenseState.Maximum.Value <= licenseState.Usage
                                               && (!licenseState.ParsedLicense.ValidTo.HasValue
                                                   || now < licenseState.ParsedLicense.ValidTo) )
            {
                // This license is full.
                continue;
            }

            Lease? lease = this.repository.CreateLease( license, userName, machine, authenticatedUserName, now, false );

            if ( lease != null )
            {
                return lease;
            }
        }

        // Third pass: the grace period.
        foreach ( License license in licenses )
        {
            LicenseState? licenseState =
                this.GetLicenseState( license, version, buildDate, now, licenseStates, errors );

            if ( licenseState == null )
            {
                continue;
            }

            if ( !licenseState.Maximum.HasValue )
            {
                // A license with no seat limit has no capacity to exceed, so there is no grace
                // period to fall back on. It reaches this pass only when the second one declined to
                // grant a lease for some other reason, such as the license having expired.
                continue;
            }

            license.GraceStartTime ??= now;

            int graceLimit = (int) Math.Ceiling(
                licenseState.Maximum.Value * (100.0 + licenseState.ParsedLicense.GracePercent) / 100.0 );

            DateTime graceEnd = license.GraceStartTime.Value.AddDays( licenseState.ParsedLicense.GraceDays );

            if ( license.GraceStartTime <= now && graceEnd > now && licenseState.Usage < graceLimit )
            {
                if ( license.GraceLastWarningTime.GetValueOrDefault( DateTime.MinValue )
                        .AddDays( this.settings.GracePeriodWarningDays ) < now )
                {
                    string body = string.Format(
                        "The license #{0} has a capacity of {1} concurrent user(s), but {2} users are currently using the product {3}. "
                        + "The grace period has started on {4} and will end on {5}. After this date, additional leases will be denied."
                        + "Please contact PostSharp Technologies to acquire additional licenses.",
                        license.LicenseId,
                        licenseState.Maximum,
                        licenseState.Usage + 1,
                        licenseState.ParsedLicense.Product,
                        license.GraceStartTime,
                        graceEnd );

                    await this.SendEmailAsync(
                        this.settings.GracePeriodWarningEmailTo,
                        this.settings.GracePeriodWarningEmailCC,
                        "WARNING: licensing capacity exceeded",
                        body,
                        cancellationToken );

                    // Recorded whether or not the message was delivered, so that a broken SMTP
                    // server cannot turn every request into a new warning email.
                    license.GraceLastWarningTime = now;
                }

                Lease? lease = this.repository.CreateLease(
                    license,
                    userName,
                    machine,
                    authenticatedUserName,
                    now,
                    true );

                if ( lease != null )
                {
                    return lease;
                }
            }
        }

        await this.SendEmailAsync(
            this.settings.DeniedRequestEmailTo,
            null,
            "ERROR: license request denied",
            string.Format(
                "No license with free capacity was found to satisfy the lease request for the product {0} from "
                + "the user '{1}' (authentication: '{2}'), machine '{3}'. " + string.Join( ". ", errors.Values ),
                productCode,
                userName,
                authenticatedUserName,
                machine ),
            cancellationToken );

        return null;
    }

    /// <summary>
    /// Determines whether a machine is a build agent, ignoring the unique-identifier suffix that
    /// build agents append to their name.
    /// </summary>
    public bool IsBuildServer( string machineName )
    {
        machineName = ComputerUniqueIdRegex.Replace( machineName, string.Empty );

        return this.buildServers.Contains( machineName );
    }

    private async Task SendEmailAsync(
        string? to,
        string? cc,
        string subject,
        string body,
        CancellationToken cancellationToken )
    {
        if ( string.IsNullOrWhiteSpace( to ) )
        {
            return;
        }

        try
        {
            await this.emailSender.SendAsync( new EmailMessage( to.Trim( ' ', '\n', '\r', '\t' ), cc, subject, body ),
                cancellationToken );
        }
        catch ( Exception e )
        {
            // A notification that cannot be delivered must never deny a developer their license.
            this.logger.LogError( e, "Cannot send the notification email '{Subject}'.", subject );
        }
    }

    /// <summary>
    /// The capacity and current usage of a license, computed lazily because most requests are served
    /// by the first pass and never need it.
    /// </summary>
    private sealed class LicenseState(
        DateTime time,
        ILeaseRepository repository,
        License license,
        LicenseInfo parsedLicense )
    {
        private int usage = -1;

        public int Usage
        {
            get
            {
                if ( this.usage == -1 )
                {
                    this.usage = repository.GetActiveLeads( license.LicenseId, time );
                }

                return this.usage;
            }
        }

        public int? Maximum => parsedLicense.UserNumber;

        [SuppressMessage( "ReSharper", "UnusedMember.Local", Justification = "Part of the state's contract." )]
        public bool InExcess => this.Maximum.HasValue && this.Maximum.Value < this.Usage;

        public LicenseInfo ParsedLicense => parsedLicense;
    }
}

/// <summary>
/// The lease granted to a client: which license key to use, and for how long.
/// </summary>
public sealed record GrantedLease( string LicenseKey, DateTime StartTime, DateTime EndTime, DateTime RenewTime );
