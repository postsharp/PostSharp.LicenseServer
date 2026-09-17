using System.Security.Cryptography;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using SharpCrafters.Backstage.LicenseServer.Options;

namespace SharpCrafters.Backstage.LicenseServer.Security;

/// <summary>
/// Supplies the audit signing key, taking it from configuration when set, and otherwise generating
/// one on first use and storing it next to the application.
/// </summary>
/// <remarks>
/// The key file must be preserved across redeployments and included in backups. Losing it does not
/// invalidate existing rows, but it does start a new signature chain.
/// </remarks>
public sealed class FileAuditKeyProvider : IAuditKeyProvider
{
    private const int keySizeInBytes = 32;

    private readonly string keyFilePath;
    private readonly LicenseServerOptions options;
    private readonly ILogger<FileAuditKeyProvider> logger;
    private readonly Lock sync = new();

    private byte[]? key;

    public FileAuditKeyProvider(
        IOptions<LicenseServerOptions> options,
        ILogger<FileAuditKeyProvider> logger,
        string keyFilePath )
    {
        this.options = options.Value;
        this.logger = logger;
        this.keyFilePath = keyFilePath;
    }

    public byte[] GetKey()
    {
        if ( this.key != null )
        {
            return this.key;
        }

        lock ( this.sync )
        {
            return this.key ??= this.LoadOrCreateKey();
        }
    }

    private byte[] LoadOrCreateKey()
    {
        if ( !string.IsNullOrWhiteSpace( this.options.AuditHmacKey ) )
        {
            return Convert.FromBase64String( this.options.AuditHmacKey );
        }

        if ( File.Exists( this.keyFilePath ) )
        {
            return Convert.FromBase64String( File.ReadAllText( this.keyFilePath ).Trim() );
        }

        byte[] newKey = RandomNumberGenerator.GetBytes( keySizeInBytes );

        Directory.CreateDirectory( Path.GetDirectoryName( this.keyFilePath )! );
        File.WriteAllText( this.keyFilePath, Convert.ToBase64String( newKey ) );

        this.logger.LogInformation(
            "Generated a new audit signing key in {Path}. Include this file in your backups and preserve "
            + "it across upgrades, otherwise the audit log signature chain restarts.",
            this.keyFilePath );

        return newKey;
    }
}
