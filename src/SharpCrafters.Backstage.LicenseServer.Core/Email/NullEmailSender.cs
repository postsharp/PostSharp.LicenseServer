namespace SharpCrafters.Backstage.LicenseServer.Email;

/// <summary>
/// Discards notification emails. Used when SMTP is disabled and when generating demo data.
/// </summary>
public sealed class NullEmailSender : IEmailSender
{
    public Task SendAsync( EmailMessage message, CancellationToken cancellationToken = default )
        => Task.CompletedTask;
}
