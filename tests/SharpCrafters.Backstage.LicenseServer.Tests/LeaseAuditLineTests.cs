using Microsoft.EntityFrameworkCore;
using System.Globalization;
using SharpCrafters.Backstage.LicenseServer.Data;
using SharpCrafters.Backstage.LicenseServer.Tests.Infrastructure;

namespace SharpCrafters.Backstage.LicenseServer.Tests;

/// <summary>
/// The audit-log line is a serialization contract: exported files are archived by customers and
/// compared across years, so the format is pinned here against literal expected strings rather than
/// against a re-implementation of the same logic.
/// </summary>
public sealed class LeaseAuditLineTests
{
    // Hashes produced by CryptoUtilities.ComputeStringHash64, which is what anonymises the names.
    private const string aliceHash = "f5cb4b18b2e28463";
    private const string desktop1Hash = "da7251349d0ffa49";

    private static Lease CreateLease()
        => new()
        {
            LeaseId = 42,
            OverwrittenLeaseId = 41,
            LicenseId = 7,
            StartTime = new DateTime( 2026, 1, 5, 9, 0, 0, DateTimeKind.Utc ),
            EndTime = new DateTime( 2026, 1, 8, 9, 0, 0, DateTimeKind.Utc ),
            UserName = "alice",
            Machine = "desktop-1",
            AuthenticatedUser = "DOMAIN\\alice",
            HMAC = "SIGNATURE=="
        };

    [Fact]
    public void Write_WithoutHmac_ProducesTheExpectedLine()
    {
        Assert.Equal(
            $"42;41;7;2026-01-05T09:00:00Z;2026-01-08T09:00:00Z;{desktop1Hash};{aliceHash}",
            CreateLease().ToAuditLine( false ) );
    }

    [Fact]
    public void Write_WithHmac_AppendsTheSignature()
    {
        Assert.Equal(
            $"42;41;7;2026-01-05T09:00:00Z;2026-01-08T09:00:00Z;{desktop1Hash};{aliceHash};SIGNATURE==",
            CreateLease().ToAuditLine( true ) );
    }

    [Fact]
    public void Write_NoOverwrittenLease_LeavesTheFieldEmpty()
    {
        Lease lease = CreateLease();
        lease.OverwrittenLeaseId = null;

        Assert.Equal(
            $"42;;7;2026-01-05T09:00:00Z;2026-01-08T09:00:00Z;{desktop1Hash};{aliceHash}",
            lease.ToAuditLine( false ) );
    }

    [Fact]
    public void Write_NeverDisclosesTheUserOrMachineName()
    {
        string line = CreateLease().ToAuditLine( true );

        Assert.DoesNotContain( "alice", line, StringComparison.OrdinalIgnoreCase );
        Assert.DoesNotContain( "desktop", line, StringComparison.OrdinalIgnoreCase );
    }

    /// <summary>
    /// Timestamps must be absolute, whatever time zone the server keeps.
    /// </summary>
    /// <remarks>
    /// The legacy implementation serialized values whose <see cref="DateTimeKind"/> was
    /// <see cref="DateTimeKind.Unspecified"/> -- which is what SQL Server returns -- in a mode that
    /// treated them as local time and shifted them, so the exported file depended on the machine
    /// that produced it.
    /// </remarks>
    [Fact]
    public void Write_EmitsAbsoluteTimestamps()
    {
        string[] fields = CreateLease().ToAuditLine( false ).Split( ';' );

        Assert.EndsWith( "Z", fields[3], StringComparison.Ordinal );
        Assert.EndsWith( "Z", fields[4], StringComparison.Ordinal );
    }

    /// <summary>
    /// A lease that has been through the database must still serialize as UTC. This is the half of
    /// the guarantee that lives in the model rather than in <c>Lease.Write</c>.
    /// </summary>
    [Fact]
    public async Task Write_AfterReload_StillEmitsUtc()
    {
        await using LicenseServerTestContext context = await LicenseServerTestContext.CreateAsync();
        License license = LicenseBuilder.Default().AddTo( context );
        Lease saved = LeaseBuilder.For( license ).From( TestClock.Origin ).Lasting( 3 ).AddTo( context );

        await using LicenseServerDbContext reader = context.CreateFreshContext();
        Lease reloaded = await reader.Leases.SingleAsync( l => l.LeaseId == saved.LeaseId );

        Assert.Equal( DateTimeKind.Utc, reloaded.StartTime.Kind );
        Assert.Equal( DateTimeKind.Utc, reloaded.EndTime.Kind );
        Assert.Equal( saved.ToAuditLine( false ), reloaded.ToAuditLine( false ) );
    }

    [Fact]
    public void Write_UsesInvariantFormatting()
    {
        CultureInfo original = CultureInfo.CurrentCulture;

        try
        {
            CultureInfo.CurrentCulture = new CultureInfo( "de-DE" );

            Assert.Equal(
                $"42;41;7;2026-01-05T09:00:00Z;2026-01-08T09:00:00Z;{desktop1Hash};{aliceHash}",
                CreateLease().ToAuditLine( false ) );
        }
        finally
        {
            CultureInfo.CurrentCulture = original;
        }
    }

}
