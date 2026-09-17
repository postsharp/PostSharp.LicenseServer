namespace PostSharp.LicenseServer.Options;

/// <summary>
/// Settings of the SMTP server used to send notification emails. Replaces the
/// <c>system.net/mailSettings</c> section of the legacy <c>Web.config</c>.
/// </summary>
public sealed class SmtpOptions
{
    public const string SectionName = "Smtp";

    /// <summary>
    /// Gets or sets a value indicating whether notification emails are sent at all.
    /// </summary>
    public bool Enabled { get; set; } = true;

    public string Host { get; set; } = "localhost";

    public int Port { get; set; } = 25;

    public bool EnableSsl { get; set; }

    /// <summary>
    /// Gets or sets the sender address. Previously hard-coded to <c>sales@postsharp.net</c>.
    /// </summary>
    public string FromAddress { get; set; } = "sales@postsharp.net";

    public string? UserName { get; set; }

    public string? Password { get; set; }
}
