using Microsoft.EntityFrameworkCore;
using SharpCrafters.Backstage.LicenseServer.Tests.Infrastructure;

namespace SharpCrafters.Backstage.LicenseServer.Tests;

/// <summary>
/// The audit log is a chain: each lease is signed together with the signature of the lease before
/// it, so removing or altering a row breaks every signature after it.
/// </summary>
/// <remarks>
/// These assertions describe behaviour that no document describes, and that a review of the
/// migration diff does not show. This is the reason why the tests state it explicitly.
/// </remarks>
public sealed class LeaseSignatureTests
{
    private static string[] Fields( string payload ) => payload.Split( ';' );

    /// <summary>
    /// The payload of the signature applied to a given lease.
    /// </summary>
    private static string PayloadFor( LicenseServerTestContext context, int leaseId )
        => context.Signer.Payloads.Single( p => Fields( p )[1] == leaseId.ToString() );

    [Fact]
    public async Task Signature_ChainsFromThePreviousLease()
    {
        await using LicenseServerTestContext context = await LicenseServerTestContext.CreateAsync();
        License license = LicenseBuilder.Default().WithUsers( 10 ).AddTo( context );
        Lease first = LeaseBuilder.For( license ).AddTo( context );

        context.Signer.Clear();
        context.Repository.CreateLease( license, "bob", "desktop-2", "bob", TestClock.Days( 1 ), false );
        await context.Repository.SaveChangesAsync();

        Assert.StartsWith( first.HMAC + ";", context.Signer.LastPayload!, StringComparison.Ordinal );
    }

    [Fact]
    public async Task Signature_FirstLeaseEver_ChainsFromAnEmptySignature()
    {
        await using LicenseServerTestContext context = await LicenseServerTestContext.CreateAsync();
        License license = LicenseBuilder.Default().AddTo( context );

        context.Signer.Clear();
        context.Repository.CreateLease( license, "alice", "desktop-1", "alice", TestClock.Origin, false );
        await context.Repository.SaveChangesAsync();

        Assert.StartsWith( ";", context.Signer.LastPayload!, StringComparison.Ordinal );
    }

    /// <summary>
    /// The leases saved together chain to each other, and not all to the same predecessor. If they
    /// chained to the same predecessor, one of them could be removed without invalidating a later
    /// signature.
    /// </summary>
    [Fact]
    public async Task Signature_LeasesSavedTogether_ChainToEachOther()
    {
        await using LicenseServerTestContext context = await LicenseServerTestContext.CreateAsync();
        License license = LicenseBuilder.Default().WithUsers( 10 ).AddTo( context );

        context.Repository.CreateLease( license, "alice", "desktop-1", "alice", TestClock.Origin, false );
        context.Repository.CreateLease( license, "bob", "desktop-2", "bob", TestClock.Origin, false );
        context.Repository.CreateLease( license, "carol", "desktop-3", "carol", TestClock.Origin, false );
        await context.Repository.SaveChangesAsync();

        List<Lease> leases = await context.CreateFreshContext().Leases.OrderBy( l => l.LeaseId ).ToListAsync();

        Assert.Equal( 3, leases.Count );

        // Each lease's payload opens with the signature of the one before it.
        for ( int i = 1; i < leases.Count; i++ )
        {
            Assert.StartsWith(
                leases[i - 1].HMAC + ";",
                PayloadFor( context, leases[i].LeaseId ),
                StringComparison.Ordinal );
        }
    }

    /// <summary>
    /// The signature covers the identifier that the database assigned to the lease, so an auditor can
    /// recompute the chain from an exported file. A signature computed before the insert would
    /// contain a zero in that position.
    /// </summary>
    [Fact]
    public async Task Signature_CoversTheAssignedLeaseId()
    {
        await using LicenseServerTestContext context = await LicenseServerTestContext.CreateAsync();
        License license = LicenseBuilder.Default().AddTo( context );

        context.Signer.Clear();
        Lease? lease = context.Repository.CreateLease( license, "alice", "desktop-1", "alice", TestClock.Origin, false );
        await context.Repository.SaveChangesAsync();

        Assert.NotEqual( 0, lease!.LeaseId );
        Assert.Equal( lease.LeaseId.ToString(), Fields( context.Signer.LastPayload! )[1] );
    }

    /// <summary>
    /// An exported line and the previous signature reproduce the signature of that line, so a
    /// modified row can be detected. This property is the purpose of the chain.
    /// </summary>
    [Fact]
    public async Task Signature_CanBeRecomputedFromTheExportedLine()
    {
        await using LicenseServerTestContext context = await LicenseServerTestContext.CreateAsync();
        License license = LicenseBuilder.Default().WithUsers( 10 ).AddTo( context );

        LeaseBuilder.For( license ).User( "alice" ).AddTo( context );
        LeaseBuilder.For( license ).User( "bob" ).Machine( "desktop-2" ).AddTo( context );

        List<Lease> leases = await context.CreateFreshContext().Leases.OrderBy( l => l.LeaseId ).ToListAsync();

        string? previous = null;

        foreach ( Lease lease in leases )
        {
            Assert.Equal( lease.HMAC, context.Repository.ComputeSignature( previous, lease ) );
            previous = lease.HMAC;
        }
    }

    [Fact]
    public async Task Signature_AlteredLease_NoLongerMatches()
    {
        await using LicenseServerTestContext context = await LicenseServerTestContext.CreateAsync();
        License license = LicenseBuilder.Default().AddTo( context );
        Lease lease = LeaseBuilder.For( license ).AddTo( context );

        string recorded = lease.HMAC!;
        lease.Machine = "somebody-elses-machine";

        Assert.NotEqual( recorded, context.Repository.ComputeSignature( null, lease ) );
    }

    [Fact]
    public async Task CreateLease_SignedPayloadCarriesTheLicenseId()
    {
        await using LicenseServerTestContext context = await LicenseServerTestContext.CreateAsync();
        License license = LicenseBuilder.Default().WithLicenseId( 7 ).AddTo( context );

        context.Signer.Clear();
        context.Repository.CreateLease( license, "alice", "desktop-1", "alice", TestClock.Origin, false );
        await context.Repository.SaveChangesAsync();

        Assert.Equal( "7", Fields( context.Signer.LastPayload! )[3] );
    }

    [Fact]
    public async Task ProlongLease_SignedPayloadCarriesTheOverwrittenLeaseId()
    {
        await using LicenseServerTestContext context = await LicenseServerTestContext.CreateAsync();
        License license = LicenseBuilder.Default().AddTo( context );
        Lease original = LeaseBuilder.For( license ).AddTo( context );

        context.Signer.Clear();
        context.Repository.ProlongLease( original, "alice", TestClock.Days( 2.5 ) );
        await context.Repository.SaveChangesAsync();

        Assert.Equal( original.LeaseId.ToString(), Fields( context.Signer.LastPayload! )[2] );
    }

    [Fact]
    public async Task CreateLease_SignedPayloadHasNoOverwrittenLeaseId()
    {
        await using LicenseServerTestContext context = await LicenseServerTestContext.CreateAsync();
        License license = LicenseBuilder.Default().AddTo( context );

        context.Signer.Clear();
        context.Repository.CreateLease( license, "alice", "desktop-1", "alice", TestClock.Origin, false );
        await context.Repository.SaveChangesAsync();

        Assert.Equal( "", Fields( context.Signer.LastPayload! )[2] );
    }

    [Fact]
    public async Task Signature_IsDeterministic()
    {
        await using LicenseServerTestContext context = await LicenseServerTestContext.CreateAsync();
        License license = LicenseBuilder.Default().AddTo( context );
        Lease lease = LeaseBuilder.For( license ).AddTo( context );

        Assert.Equal(
            context.Repository.ComputeSignature( null, lease ),
            context.Repository.ComputeSignature( null, lease ) );
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

    /// <summary>
    /// A lease is never readable without its signature, so the insert and the signature run in one
    /// transaction.
    /// </summary>
    [Fact]
    public async Task Signature_EveryPersistedLeaseIsSigned()
    {
        await using LicenseServerTestContext context = await LicenseServerTestContext.CreateAsync();
        License license = LicenseBuilder.Default().WithUsers( 10 ).AddTo( context );

        for ( int i = 0; i < 5; i++ )
        {
            context.Repository.CreateLease( license, $"user{i}", $"machine-{i}", $"user{i}", TestClock.Origin, false );
        }

        await context.Repository.SaveChangesAsync();

        Assert.Empty( await context.CreateFreshContext().Leases.Where( l => l.HMAC == null ).ToListAsync() );
    }
}
