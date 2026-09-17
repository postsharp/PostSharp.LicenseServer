using PostSharp.LicenseServer.Tests.Infrastructure;

namespace PostSharp.LicenseServer.Tests;

/// <summary>
/// The audit log is a chain: each lease is signed together with the signature of the previously
/// persisted lease, so removing or altering a row breaks every signature after it.
/// </summary>
/// <remarks>
/// These assertions encode behaviour that is documented nowhere and that is invisible in a code
/// review of the migration diff, which is why they are pinned explicitly.
/// </remarks>
public sealed class LeaseSignatureTests
{
    private static string[] Fields( string payload ) => payload.Split( ';' );

    [Fact]
    public async Task GetSignature_ChainsFromThePreviousLease()
    {
        await using LicenseServerTestContext context = await LicenseServerTestContext.CreateAsync();
        License license = LicenseBuilder.Default().AddTo( context );
        Lease first = LeaseBuilder.For( license ).AddTo( context );

        context.Signer.Clear();
        context.Repository.CreateLease( license, "bob", "desktop-2", "bob", TestClock.Days( 1 ), false );

        Assert.StartsWith( first.HMAC + ";", context.Signer.LastPayload!, StringComparison.Ordinal );
    }

    [Fact]
    public async Task GetSignature_FirstLeaseEver_ChainsFromAnEmptySignature()
    {
        await using LicenseServerTestContext context = await LicenseServerTestContext.CreateAsync();
        License license = LicenseBuilder.Default().AddTo( context );

        context.Signer.Clear();
        context.Repository.CreateLease( license, "alice", "desktop-1", "alice", TestClock.Origin, false );

        Assert.StartsWith( ";", context.Signer.LastPayload!, StringComparison.Ordinal );
    }

    /// <summary>
    /// The chain is anchored to what is committed, not to what is pending. Several leases created in
    /// one unit of work therefore all chain from the same predecessor.
    /// </summary>
    [Fact]
    public async Task GetSignature_DoesNotSeePendingInserts()
    {
        await using LicenseServerTestContext context = await LicenseServerTestContext.CreateAsync();
        License license = LicenseBuilder.Default().WithUsers( 10 ).AddTo( context );
        Lease committed = LeaseBuilder.For( license ).AddTo( context );

        context.Signer.Clear();
        context.Repository.CreateLease( license, "bob", "desktop-2", "bob", TestClock.Days( 1 ), false );
        context.Repository.CreateLease( license, "carol", "desktop-3", "carol", TestClock.Days( 1 ), false );

        Assert.Equal( 2, context.Signer.Payloads.Count );
        Assert.All(
            context.Signer.Payloads,
            payload => Assert.StartsWith( committed.HMAC + ";", payload, StringComparison.Ordinal ) );
    }

    [Fact]
    public async Task GetSignature_AfterSave_ChainsFromTheNewlyPersistedLease()
    {
        await using LicenseServerTestContext context = await LicenseServerTestContext.CreateAsync();
        License license = LicenseBuilder.Default().WithUsers( 10 ).AddTo( context );
        LeaseBuilder.For( license ).AddTo( context );

        Lease? second = context.Repository.CreateLease( license, "bob", "desktop-2", "bob", TestClock.Days( 1 ), false );
        await context.Repository.SaveChangesAsync();

        context.Signer.Clear();
        context.Repository.CreateLease( license, "carol", "desktop-3", "carol", TestClock.Days( 1 ), false );

        Assert.StartsWith( second!.HMAC + ";", context.Signer.LastPayload!, StringComparison.Ordinal );
    }

    /// <summary>
    /// A lease is signed before it is inserted, so its own identifier is not yet known.
    /// </summary>
    [Fact]
    public async Task GetSignature_SignedPayloadCarriesLeaseIdZero()
    {
        await using LicenseServerTestContext context = await LicenseServerTestContext.CreateAsync();
        License license = LicenseBuilder.Default().AddTo( context );

        context.Signer.Clear();
        context.Repository.CreateLease( license, "alice", "desktop-1", "alice", TestClock.Origin, false );

        // Field 0 is the chained signature, so the lease's own fields start at index 1.
        Assert.Equal( "0", Fields( context.Signer.LastPayload! )[1] );
    }

    /// <summary>
    /// Regression guard: EF Core does not populate a foreign key from a navigation property until
    /// the entity is tracked, and leases are signed before that. If the key were left unassigned the
    /// license would silently be signed as zero.
    /// </summary>
    [Fact]
    public async Task CreateLease_SignedPayloadCarriesTheLicenseId()
    {
        await using LicenseServerTestContext context = await LicenseServerTestContext.CreateAsync();
        License license = LicenseBuilder.Default().WithLicenseId( 7 ).AddTo( context );

        context.Signer.Clear();
        context.Repository.CreateLease( license, "alice", "desktop-1", "alice", TestClock.Origin, false );

        Assert.Equal( "7", Fields( context.Signer.LastPayload! )[3] );
    }

    /// <summary>
    /// The same regression guard, for the self-referencing key that makes the log an append-only
    /// chain of replacements.
    /// </summary>
    [Fact]
    public async Task ProlongLease_SignedPayloadCarriesTheOverwrittenLeaseId()
    {
        await using LicenseServerTestContext context = await LicenseServerTestContext.CreateAsync();
        License license = LicenseBuilder.Default().AddTo( context );
        Lease original = LeaseBuilder.For( license ).AddTo( context );

        context.Signer.Clear();
        context.Repository.ProlongLease( original, "alice", TestClock.Days( 2.5 ) );

        Assert.Equal( original.LeaseId.ToString(), Fields( context.Signer.LastPayload! )[2] );
    }

    [Fact]
    public async Task CreateLease_SignedPayloadHasNoOverwrittenLeaseId()
    {
        await using LicenseServerTestContext context = await LicenseServerTestContext.CreateAsync();
        License license = LicenseBuilder.Default().AddTo( context );

        context.Signer.Clear();
        context.Repository.CreateLease( license, "alice", "desktop-1", "alice", TestClock.Origin, false );

        Assert.Equal( "", Fields( context.Signer.LastPayload! )[2] );
    }

    /// <summary>
    /// The whole point of replacing the randomly-keyed HMAC: signing the same content twice now
    /// yields the same signature, so the chain can actually be verified.
    /// </summary>
    [Fact]
    public async Task Signature_IsDeterministic()
    {
        await using LicenseServerTestContext context = await LicenseServerTestContext.CreateAsync();
        License license = LicenseBuilder.Default().AddTo( context );

        Lease lease = new()
        {
            License = license,
            LicenseId = license.LicenseId,
            UserName = "alice",
            Machine = "desktop-1",
            AuthenticatedUser = "alice",
            StartTime = TestClock.Origin,
            EndTime = TestClock.Days( 3 )
        };

        Assert.Equal( context.Repository.GetSignature( lease ), context.Repository.GetSignature( lease ) );
    }

    [Fact]
    public async Task Signature_DiffersWhenTheLeaseDiffers()
    {
        await using LicenseServerTestContext context = await LicenseServerTestContext.CreateAsync();
        License license = LicenseBuilder.Default().AddTo( context );

        Lease lease = new()
        {
            License = license,
            LicenseId = license.LicenseId,
            UserName = "alice",
            Machine = "desktop-1",
            AuthenticatedUser = "alice",
            StartTime = TestClock.Origin,
            EndTime = TestClock.Days( 3 )
        };

        string before = context.Repository.GetSignature( lease );
        lease.Machine = "desktop-2";

        Assert.NotEqual( before, context.Repository.GetSignature( lease ) );
    }

    [Fact]
    public async Task Signature_FitsTheDatabaseColumn()
    {
        await using LicenseServerTestContext context = await LicenseServerTestContext.CreateAsync();
        License license = LicenseBuilder.Default().AddTo( context );
        Lease lease = LeaseBuilder.For( license ).AddTo( context );

        // The HMAC column is varchar(100) and is not being widened by this migration.
        Assert.NotNull( lease.HMAC );
        Assert.InRange( lease.HMAC.Length, 1, 100 );
    }
}
