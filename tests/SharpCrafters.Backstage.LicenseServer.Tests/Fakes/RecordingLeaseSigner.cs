using System.Security.Cryptography;
using System.Text;
using SharpCrafters.Backstage.LicenseServer.Security;

namespace SharpCrafters.Backstage.LicenseServer.Tests.Fakes;

/// <summary>
/// Produces a signature that depends only on the payload, and stores every payload it signed, so
/// that a test can assert on the content of the audit signature chain.
/// </summary>
public sealed class RecordingLeaseSigner : ILeaseSigner
{
    private static readonly byte[] key = Encoding.UTF8.GetBytes( "test-audit-signing-key" );

    private readonly List<string> payloads = [];

    /// <summary>
    /// Gets the payloads signed so far, in order.
    /// </summary>
    public IReadOnlyList<string> Payloads => this.payloads;

    public string? LastPayload => this.payloads.Count == 0 ? null : this.payloads[^1];

    public string Sign( string payload )
    {
        this.payloads.Add( payload );

        return Convert.ToBase64String( HMACSHA256.HashData( key, Encoding.UTF8.GetBytes( payload ) ) );
    }

    public void Clear() => this.payloads.Clear();
}
