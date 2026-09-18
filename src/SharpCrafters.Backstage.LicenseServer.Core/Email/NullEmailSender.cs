// Copyright (c) SharpCrafters s.r.o. See the LICENSE.md file in the root directory of this repository root for details.

namespace SharpCrafters.Backstage.LicenseServer.Email;

/// <summary>
/// Discards the notification e-mails. The server uses it when SMTP is disabled and when it generates
/// demonstration data.
/// </summary>
public sealed class NullEmailSender : IEmailSender
{
    public Task SendAsync( EmailMessage message, CancellationToken cancellationToken = default ) => Task.CompletedTask;
}