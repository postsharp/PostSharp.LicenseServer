// Copyright (c) SharpCrafters s.r.o. See the LICENSE.md file in the root directory of this repository root for details.

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
/// The allocation runs three passes over the candidate licenses. The first pass reuses or prolongs a
/// lease that the user already holds. The second pass grants a new lease against free capacity. The
/// third pass grants a lease within the grace period.
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
            foreach ( var buildServer in this.settings.BuildServers.Split( [';', ',', ' '] ) )
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
        if ( cache.TryGetValue( license.LicenseId, out var licenseState ) )
        {
            return licenseState;
        }

        var parsedLicense = this.licenseParser.TryParse( license.LicenseKey );

        if ( parsedLicense == null )
        {
            errors[license.LicenseId] = $"The license key #{license.LicenseId} is invalid.";

            return null;
        }

        if ( parsedLicense.MinPostSharpVersion > this.serverVersion.LicensingLibraryVersion )
        {
            errors[license.LicenseId] =
                $"The license #{license.LicenseId} requires a higher version of the licensing library on the License Server. "
                + $"Please upgrade the License Server to >= {parsedLicense.MinPostSharpVersion.Major}."
                + $"{parsedLicense.MinPostSharpVersion.Minor}.{parsedLicense.MinPostSharpVersion.Build}";

            return null;
        }

        // A PostSharp license names the lowest version of PostSharp that can read it, and a Metalama
        // license names the lowest version of Metalama. The two are independent, so the minimum that
        // applies is the one of the family of the licensed product.
        var minClientVersion = parsedLicense.MinClientVersion;

        if ( minClientVersion > version )
        {
            errors[license.LicenseId] =
                $"The license #{license.LicenseId} of type {parsedLicense.LicenseType} requires "
                + $"{parsedLicense.ClientName} version >= {minClientVersion.Major}.{minClientVersion.Minor}."
                + $"{minClientVersion.Build} but the requested version is "
                + $"{version.Major}.{version.Minor}.{version.Build}.";

            return null;
        }

        if ( !parsedLicense.IsLicenseServerEligible )
        {
            errors[license.LicenseId] =
                $"The license #{license.LicenseId}, of type {parsedLicense.LicenseType}, cannot be used in the license server.";

            return null;
        }

        if ( !( buildDate == null || parsedLicense.SubscriptionEndDate == null
                                  || buildDate <= parsedLicense.SubscriptionEndDate ) )
        {
            // The version number was introduced in the license server protocol in PostSharp 5.
            errors[license.LicenseId] = version.Major >= 5
                ? $"The maintenance subscription of license #{license.LicenseId} ends on "
                  + $"{parsedLicense.SubscriptionEndDate:d} but the requested version "
                  + $"{version.Major}.{version.Minor}.{version.Build} has been built on {buildDate:d}."
                : $"The maintenance subscription of license #{license.LicenseId} ends on "
                  + $"{parsedLicense.SubscriptionEndDate:d} but the requested version has been built on "
                  + $"{buildDate:d}.";

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
        // A client that names no product is served from any pool. Every PostSharp client relies on
        // this behaviour, because none of them sends the argument.
        var productCodes = string.IsNullOrEmpty( productCode )
            ? []
            : ProductCodes.Matching( productCode );

        var licenses = await this.repository.Licenses
            .Where( license => ( productCodes.Count == 0 || productCodes.Contains( license.ProductCode ) )
                               && license.Priority >= 0 )
            .OrderBy( license => license.Priority )
            .ToArrayAsync( cancellationToken );

        if ( this.IsBuildServer( machine ) )
        {
            Dictionary<int, LicenseState> buildServerStates = [];

            // A build agent is exempt from consuming a seat. It is not exempt from the rules that
            // decide which licenses may be served, so the same validation runs.
            foreach ( var candidate in licenses )
            {
                var state =
                    this.GetLicenseState( candidate, version, buildDate, now, buildServerStates, errors );

                if ( state == null )
                {
                    continue;
                }

                var endTime = now.AddDays( this.settings.NewLeaseDays );

                if ( state.ParsedLicense.ValidTo.HasValue && state.ParsedLicense.ValidTo < endTime )
                {
                    endTime = state.ParsedLicense.ValidTo.Value;
                }

                if ( endTime <= now )
                {
                    // The license expires before the lease would begin.
                    continue;
                }

                // The server does not store the lease of a build server, so that build agents do not
                // consume the seats of the developers they build for.
                return new GrantedLease(
                    candidate.LicenseKey,
                    now,
                    endTime,
                    endTime.AddDays( -this.settings.MinLeaseDays ) );
            }

            // No license could be served without a lease. Continue with the normal allocation.
        }

        var lease = await this.GetLeaseAsync(
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
        foreach ( var license in licenses )
        {
            var licenseState =
                this.GetLicenseState( license, version, buildDate, now, licenseStates, errors );

            if ( licenseState == null )
            {
                continue;
            }

            var licenseId = license.LicenseId;

            var currentLeases = await this.repository.OpenLeases
                .Where( l => l.LicenseId == licenseId && l.StartTime <= now && l.EndTime > now && l.UserName == userName )
                .Include( l => l.License )
                .OrderBy( l => l.StartTime )
                .ToArrayAsync( cancellationToken );

            Dictionary<string, string> machines = new( StringComparer.OrdinalIgnoreCase );

            foreach ( var candidateLease in currentLeases )
            {
                machines[candidateLease.Machine] = candidateLease.Machine;

                if ( candidateLease.Machine != machine )
                {
                    continue;
                }

                if ( candidateLease.EndTime > now.AddDays( this.settings.MinLeaseDays ) )
                {
                    // The lease that the user already holds ends late enough.
                    return candidateLease;
                }

                // A lease can always be prolonged, because a lease starts at the current instant and
                // therefore already covers it. The license period or the grace period can still end
                // first.
                var prolonged = this.repository.ProlongLease( candidateLease, authenticatedUserName, now );

                if ( prolonged == null )
                {
                    continue;
                }

                return prolonged;
            }

            // This user holds no lease on this machine. An additional machine consumes no seat while
            // the user works on fewer machines than MachinesPerUser.
            if ( machines.Count % this.settings.MachinesPerUser != 0 )
            {
                var lease = this.repository.CreateLease(
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
        foreach ( var license in licenses )
        {
            var licenseState =
                this.GetLicenseState( license, version, buildDate, now, licenseStates, errors );

            if ( licenseState == null )
            {
                continue;
            }

            if ( licenseState.Maximum.HasValue && licenseState.Maximum.Value <= licenseState.Usage
                                               && ( !licenseState.ParsedLicense.ValidTo.HasValue
                                                    || now < licenseState.ParsedLicense.ValidTo ) )
            {
                // This license is full.
                continue;
            }

            var lease = this.repository.CreateLease( license, userName, machine, authenticatedUserName, now, false );

            if ( lease != null )
            {
                return lease;
            }
        }

        // Third pass: the grace period.
        foreach ( var license in licenses )
        {
            var licenseState =
                this.GetLicenseState( license, version, buildDate, now, licenseStates, errors );

            if ( licenseState == null )
            {
                continue;
            }

            if ( !licenseState.Maximum.HasValue )
            {
                // A license with no seat limit has no capacity to exceed, so it has no grace period.
                // It reaches this pass only when the second pass refused it for another reason, for
                // example an expired license.
                continue;
            }

            license.GraceStartTime ??= now;

            var graceLimit = LicenseCapacity.GetGraceLimit(
                licenseState.Maximum.Value,
                licenseState.ParsedLicense.GracePercent );

            var graceEnd = license.GraceStartTime.Value.AddDays( licenseState.ParsedLicense.GraceDays );

            if ( license.GraceStartTime <= now && graceEnd > now && licenseState.Usage < graceLimit )
            {
                if ( license.GraceLastWarningTime.GetValueOrDefault( DateTime.MinValue )
                        .AddDays( this.settings.GracePeriodWarningDays ) < now )
                {
                    var body =
                        $"The license #{license.LicenseId} has a capacity of {licenseState.Maximum} concurrent user(s), "
                        + $"but {licenseState.Usage + 1} users are currently using the product "
                        + $"{licenseState.ParsedLicense.Product}. "
                        + $"The grace period has started on {license.GraceStartTime} and will end on {graceEnd}. "
                        + "After this date, additional leases will be denied."
                        + "Please contact PostSharp Technologies to acquire additional licenses.";

                    await this.SendEmailAsync(
                        this.settings.GracePeriodWarningEmailTo,
                        this.settings.GracePeriodWarningEmailCC,
                        "WARNING: licensing capacity exceeded",
                        body,
                        cancellationToken );

                    // The time is recorded whether or not the message was delivered, so that an SMTP
                    // server that fails does not turn every request into a new warning e-mail.
                    license.GraceLastWarningTime = now;
                }

                var lease = this.repository.CreateLease(
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
            $"No license with free capacity was found to satisfy the lease request for the product "
            + $"{productCode} from the user '{userName}' (authentication: '{authenticatedUserName}'), "
            + $"machine '{machine}'. " + string.Join( ". ", errors.Values ),
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
            await this.emailSender.SendAsync(
                new EmailMessage( to.Trim( ' ', '\n', '\r', '\t' ), cc, subject, body ),
                cancellationToken );
        }
        catch ( Exception e )
        {
            // A notification that cannot be sent must never deny a license.
            this.logger.LogError( e, "Cannot send the notification email '{Subject}'.", subject );
        }
    }

    /// <summary>
    /// The capacity and the current usage of a license. The usage is computed on demand, because the
    /// first pass serves most requests and does not need it.
    /// </summary>
    private sealed class LicenseState
    {
        private readonly DateTime time;
        private readonly ILeaseRepository repository;
        private readonly License license;
        private readonly LicenseInfo parsedLicense;
        private int usage = -1;

        public LicenseState(
            DateTime time,
            ILeaseRepository repository,
            License license,
            LicenseInfo parsedLicense )
        {
            this.time = time;
            this.repository = repository;
            this.license = license;
            this.parsedLicense = parsedLicense;
        }

        public int Usage
        {
            get
            {
                if ( this.usage == -1 )
                {
                    this.usage = this.repository.GetActiveSeats( this.license.LicenseId, this.time );
                }

                return this.usage;
            }
        }

        public int? Maximum => this.parsedLicense.UserNumber;

        [SuppressMessage( "ReSharper", "UnusedMember.Local", Justification = "Part of the state's contract." )]
        public bool InExcess => this.Maximum.HasValue && this.Maximum.Value < this.Usage;

        public LicenseInfo ParsedLicense => this.parsedLicense;
    }
}

/// <summary>
/// The lease granted to a client: which license key to use, and for how long.
/// </summary>
public sealed record GrantedLease( string LicenseKey, DateTime StartTime, DateTime EndTime, DateTime RenewTime );