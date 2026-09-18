using Microsoft.EntityFrameworkCore;
using System.Globalization;
using SharpCrafters.Backstage.LicenseServer.Data;
using SharpCrafters.Backstage.LicenseServer.Tests.Infrastructure;

namespace SharpCrafters.Backstage.LicenseServer.Tests;

/// <summary>
/// The line of the audit log is a serialization contract. Customers archive the exported files and
/// compare them across years. These tests therefore compare the format to literal strings, and not
/// to a second implementation of the same logic.
/// </summary>
public sealed class LeaseAuditLineTests
{
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
            AuthenticatedUser = "DOMAIN\\alice"
        };

    [Fact]
    public void Write_ProducesTheExpectedLine()
    {
        Assert.Equal(
            "42;41;7;2026-01-05T09:00:00Z;2026-01-08T09:00:00Z;desktop-1;alice",
            CreateLease().ToAuditLine() );
    }

    [Fact]
    public void Write_NoOverwrittenLease_LeavesTheFieldEmpty()
    {
        Lease lease = CreateLease();
        lease.OverwrittenLeaseId = null;

        Assert.Equal(
            "42;;7;2026-01-05T09:00:00Z;2026-01-08T09:00:00Z;desktop-1;alice",
            lease.ToAuditLine() );
    }

    /// <summary>
    /// The administrator of the server reads the log to learn which user and which machine hold a
    /// seat, so both names are written as they were recorded. A version earlier than 2027.0 wrote
    /// them as hashes, which nothing could read.
    /// </summary>
    [Fact]
    public void Write_NamesTheUserAndTheMachine()
    {
        string[] fields = CreateLease().ToAuditLine().Split( ';' );

        Assert.Equal( "desktop-1", fields[5] );
        Assert.Equal( "alice", fields[6] );
    }

    /// <summary>
    /// Timestamps must be absolute, whatever time zone the server keeps.
    /// </summary>
    /// <remarks>
    /// SQL Server returns values whose <see cref="DateTimeKind"/> is
    /// <see cref="DateTimeKind.Unspecified"/>. The legacy implementation serialized them in a mode
    /// that treated them as local times and shifted them, so the exported file depended on the
    /// machine that produced it.
    /// </remarks>
    [Fact]
    public void Write_EmitsAbsoluteTimestamps()
    {
        string[] fields = CreateLease().ToAuditLine().Split( ';' );

        Assert.EndsWith( "Z", fields[3], StringComparison.Ordinal );
        Assert.EndsWith( "Z", fields[4], StringComparison.Ordinal );
    }

    /// <summary>
    /// A lease read from the database is also serialized as UTC. The value converter of the model
    /// provides this part of the guarantee, and <c>Lease.Write</c> provides the other part.
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
        Assert.Equal( saved.ToAuditLine(), reloaded.ToAuditLine() );
    }

    [Fact]
    public void Write_UsesInvariantFormatting()
    {
        CultureInfo original = CultureInfo.CurrentCulture;

        try
        {
            CultureInfo.CurrentCulture = new CultureInfo( "de-DE" );

            Assert.Equal(
                "42;41;7;2026-01-05T09:00:00Z;2026-01-08T09:00:00Z;desktop-1;alice",
                CreateLease().ToAuditLine() );
        }
        finally
        {
            CultureInfo.CurrentCulture = original;
        }
    }
}
