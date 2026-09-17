using System.Globalization;

namespace PostSharp.LicenseServer.Tests;

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
    /// Timestamps must be absolute. The legacy implementation serialized values whose
    /// <see cref="DateTimeKind"/> was <see cref="DateTimeKind.Unspecified"/> -- which is what SQL
    /// Server returns -- in a mode that treated them as local time and shifted them, so the exported
    /// file depended on the time zone of the machine that produced it.
    /// </summary>
    [Fact]
    public void Write_IsIndependentOfTheServerTimeZone()
    {
        string expected = CreateLease().ToAuditLine( false );

        foreach ( string timeZoneId in new[] { "UTC", "Pacific Standard Time", "Tokyo Standard Time" } )
        {
            if ( !TryFindTimeZone( timeZoneId, out _ ) )
            {
                continue;
            }

            // The value carries its own UTC offset, so no ambient time zone can change it.
            Assert.Equal( expected, CreateLease().ToAuditLine( false ) );
        }

        Assert.EndsWith( "Z", expected.Split( ';' )[3], StringComparison.Ordinal );
        Assert.EndsWith( "Z", expected.Split( ';' )[4], StringComparison.Ordinal );
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

    private static bool TryFindTimeZone( string id, out TimeZoneInfo? timeZone )
    {
        try
        {
            timeZone = TimeZoneInfo.FindSystemTimeZoneById( id );

            return true;
        }
        catch ( TimeZoneNotFoundException )
        {
            timeZone = null;

            return false;
        }
    }
}
