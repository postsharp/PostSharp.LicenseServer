using MailKit.Net.Smtp;
using MailKit.Security;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using MimeKit;
using SharpCrafters.Backstage.LicenseServer.Options;

namespace SharpCrafters.Backstage.LicenseServer.Email;

/// <summary>
/// Sends notification e-mails over SMTP.
/// </summary>
/// <remarks>
/// The legacy implementation called <c>SmtpClient.SendAsync(message, null)</c> and never read the
/// result, so every failure was silent. This implementation writes a failure to the log, and it
/// still raises no exception, because an SMTP server that fails must not deny a license.
/// </remarks>
public sealed class SmtpEmailSender(
    IOptions<SmtpOptions> options,
    ILogger<SmtpEmailSender> logger ) : IEmailSender
{
    private readonly SmtpOptions options = options.Value;
    private readonly ILogger<SmtpEmailSender> logger = logger;

    public async Task SendAsync( EmailMessage message, CancellationToken cancellationToken = default )
    {
        if ( !this.options.Enabled )
        {
            return;
        }

        if ( string.IsNullOrWhiteSpace( message.To ) )
        {
            return;
        }

        try
        {
            MimeMessage mimeMessage = new();
            mimeMessage.From.Add( MailboxAddress.Parse( this.options.FromAddress ) );
            mimeMessage.To.Add( MailboxAddress.Parse( message.To.Trim( ' ', '\n', '\r', '\t' ) ) );

            if ( !string.IsNullOrEmpty( message.Cc ) )
            {
                foreach ( string address in message.Cc.Split( [',', ';', ' '], StringSplitOptions.RemoveEmptyEntries ) )
                {
                    string trimmed = address.Trim( ' ', '\n', '\r', '\t' );

                    if ( trimmed.Length > 0 )
                    {
                        mimeMessage.Cc.Add( MailboxAddress.Parse( trimmed ) );
                    }
                }
            }

            mimeMessage.Subject = "[SharpCrafters License Server] " + message.Subject;
            mimeMessage.Priority = MessagePriority.Urgent;
            mimeMessage.Body = new TextPart( "plain" ) { Text = message.Body };

            using SmtpClient client = new();

            await client.ConnectAsync(
                this.options.Host,
                this.options.Port,
                this.options.EnableSsl ? SecureSocketOptions.StartTls : SecureSocketOptions.Auto,
                cancellationToken );

            if ( !string.IsNullOrEmpty( this.options.UserName ) )
            {
                await client.AuthenticateAsync( this.options.UserName, this.options.Password ?? string.Empty, cancellationToken );
            }

            await client.SendAsync( mimeMessage, cancellationToken );
            await client.DisconnectAsync( true, cancellationToken );
        }
        catch ( Exception e )
        {
            this.logger.LogError( e, "Cannot send the notification email '{Subject}' to {To}.", message.Subject, message.To );
        }
    }

}
