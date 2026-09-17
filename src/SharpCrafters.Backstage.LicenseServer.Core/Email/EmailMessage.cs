namespace SharpCrafters.Backstage.LicenseServer.Email;

/// <summary>
/// A notification email to the license administrator.
/// </summary>
public sealed record EmailMessage(
    string To,
    string? Cc,
    string Subject,
    string Body );
