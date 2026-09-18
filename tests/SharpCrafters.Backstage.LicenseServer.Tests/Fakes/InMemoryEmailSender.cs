using System.Collections.Concurrent;
using SharpCrafters.Backstage.LicenseServer.Email;

namespace SharpCrafters.Backstage.LicenseServer.Tests.Fakes;

/// <summary>
/// An <see cref="IEmailSender"/> that stores the messages in memory instead of sending them, so that
/// a test can assert on the notifications that the license server produces.
/// </summary>
public sealed class InMemoryEmailSender : IEmailSender
{
    private readonly ConcurrentQueue<EmailMessage> sent = new();

    /// <summary>
    /// Gets or sets an exception that this sender raises instead of recording the message, so that a
    /// test can verify that an SMTP server that fails never denies a license.
    /// </summary>
    public Exception? ThrowOnSend { get; set; }

    /// <summary>
    /// Gets the messages recorded so far, in the order they were sent.
    /// </summary>
    public IReadOnlyList<EmailMessage> Sent => this.sent.ToArray();

    public EmailMessage? Last => this.Sent.LastOrDefault();

    public Task SendAsync( EmailMessage message, CancellationToken cancellationToken = default )
    {
        if ( this.ThrowOnSend != null )
        {
            throw this.ThrowOnSend;
        }

        this.sent.Enqueue( message );

        return Task.CompletedTask;
    }

    public void Clear() => this.sent.Clear();

    /// <summary>
    /// Returns the messages whose subject contains the given text.
    /// </summary>
    public IReadOnlyList<EmailMessage> WithSubject( string substring )
        => this.Sent.Where( m => m.Subject.Contains( substring, StringComparison.OrdinalIgnoreCase ) ).ToArray();
}
