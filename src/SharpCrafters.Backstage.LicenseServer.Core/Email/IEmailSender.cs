namespace SharpCrafters.Backstage.LicenseServer.Email;

/// <summary>
/// Sends notification e-mails. A failure to send must never fail a lease request, so an
/// implementation writes the failure to the log and raises no exception.
/// </summary>
public interface IEmailSender
{
    Task SendAsync( EmailMessage message, CancellationToken cancellationToken = default );
}
