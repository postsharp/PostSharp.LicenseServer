namespace SharpCrafters.Backstage.LicenseServer.Email;

/// <summary>
/// A notification e-mail sent to the administrator of the licenses.
/// </summary>
public sealed record EmailMessage(
    string To,
    string? Cc,
    string Subject,
    string Body );
