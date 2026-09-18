// Copyright (c) SharpCrafters s.r.o. See the LICENSE.md file in the root directory of this repository root for details.

namespace SharpCrafters.Backstage.LicenseServer.Email;

/// <summary>
/// A notification e-mail sent to the administrator of the licenses.
/// </summary>
public sealed record EmailMessage(
    string To,
    string? Cc,
    string Subject,
    string Body );