using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using PostSharp.LicenseServer.Data;
using PostSharp.LicenseServer.Options;
using PostSharp.LicenseServer.Services;
using PostSharp.LicenseServer.Tests.Fakes;

namespace PostSharp.LicenseServer.Tests;

/// <summary>
/// Build agents are recognised by name so that their leases are not persisted and therefore do not
/// consume developer seats. Agents append a hexadecimal unique identifier to their machine name,
/// which has to be stripped before the name is matched.
/// </summary>
public sealed class BuildServerDetectionTests
{
    private static LeaseService CreateService( string? buildServers )
    {
        LicenseServerOptions options = new() { BuildServers = buildServers };

        return new LeaseService(
            new StubRepository(),
            Microsoft.Extensions.Options.Options.Create( options ),
            new FakeLicenseParser(),
            new FixedServerVersion(),
            new InMemoryEmailSender(),
            NullLogger<LeaseService>.Instance );
    }

    [Theory]
    [InlineData( "server", true )]
    [InlineData( "server-1a2b", true )]
    [InlineData( "server-ABCDEF", true )]
    [InlineData( "server-0", true )]
    [InlineData( "SERVER", true )]
    [InlineData( "Server-1A2B", true )]
    public void IsBuildServer_KnownAgent_ReturnsTrue( string machine, bool expected )
        => Assert.Equal( expected, CreateService( "server" ).IsBuildServer( machine ) );

    [Theory]
    // Only a hexadecimal suffix is stripped, so "-xyz" stays part of the name.
    [InlineData( "server-xyz" )]
    [InlineData( "myserver" )]
    [InlineData( "server2" )]
    [InlineData( "desktop-1a2b" )]
    [InlineData( "" )]
    public void IsBuildServer_OtherMachine_ReturnsFalse( string machine )
        => Assert.False( CreateService( "server" ).IsBuildServer( machine ) );

    [Theory]
    [InlineData( "build1;build2" )]
    [InlineData( "build1,build2" )]
    [InlineData( "build1 build2" )]
    [InlineData( " build1 ; build2 " )]
    public void IsBuildServer_AcceptsEverySeparatorAndTrimsWhitespace( string buildServers )
    {
        LeaseService service = CreateService( buildServers );

        Assert.True( service.IsBuildServer( "build1" ) );
        Assert.True( service.IsBuildServer( "build2-ff01" ) );
        Assert.False( service.IsBuildServer( "build3" ) );
    }

    [Theory]
    [InlineData( null )]
    [InlineData( "" )]
    [InlineData( "   " )]
    public void IsBuildServer_NoBuildServersConfigured_ReturnsFalse( string? buildServers )
        => Assert.False( CreateService( buildServers ).IsBuildServer( "server" ) );

    /// <summary>
    /// A repository that is never reached: these tests only exercise name matching, which happens
    /// before any database access.
    /// </summary>
    private sealed class StubRepository : ILeaseRepository
    {
        public IQueryable<Lease> OpenLeases => Array.Empty<Lease>().AsQueryable();

        public IQueryable<License> Licenses => Array.Empty<License>().AsQueryable();

        public Lease? CreateLease(
            License license,
            string user,
            string machine,
            string authenticatedUserName,
            DateTime time,
            bool grace )
            => throw new NotSupportedException();

        public Lease? ProlongLease( Lease oldLease, string authenticatedUserName, DateTime time )
            => throw new NotSupportedException();

        public void CancelLease( Lease lease, string authenticatedUserName, DateTime time )
            => throw new NotSupportedException();

        public int GetActiveLeads( int licenseId, DateTime dateTime ) => throw new NotSupportedException();

        public IEnumerable<LeaseCountingPoint> GetLeaseCountingPoints(
            int licenseId,
            DateTime startTime,
            DateTime endTime )
            => throw new NotSupportedException();

        public Task<int> SaveChangesAsync( CancellationToken cancellationToken = default )
            => throw new NotSupportedException();

        public int SaveChanges() => throw new NotSupportedException();
    }
}
