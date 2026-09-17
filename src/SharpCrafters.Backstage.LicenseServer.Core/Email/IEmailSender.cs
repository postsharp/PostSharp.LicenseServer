namespace SharpCrafters.Backstage.LicenseServer.Email;

/// <summary>
/// Sends notification emails. A failure to send must never fail a lease request, so implementations
/// are expected to log rather than throw.
/// </summary>
public interface IEmailSender
{
    Task SendAsync( EmailMessage message, CancellationToken cancellationToken = default );
}
