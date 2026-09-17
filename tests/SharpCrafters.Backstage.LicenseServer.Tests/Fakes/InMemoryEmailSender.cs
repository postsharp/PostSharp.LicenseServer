using System.Collections.Concurrent;
using PostSharp.LicenseServer.Email;

namespace PostSharp.LicenseServer.Tests.Fakes;

/// <summary>
/// An <see cref="IEmailSender"/> that keeps the messages in memory instead of sending them, so that
/// tests can assert on what the license server would have notified the administrator about.
/// </summary>
public sealed class InMemoryEmailSender : IEmailSender
{
    private readonly ConcurrentQueue<EmailMessage> sent = new();

    /// <summary>
    /// Gets or sets an exception to throw instead of recording the message, to verify that a broken
    /// SMTP server never denies a developer their license.
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
