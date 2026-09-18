// Copyright (c) SharpCrafters s.r.o. See the LICENSE.md file in the root directory of this repository root for details.

using System.Runtime.InteropServices;
using System.Text.Encodings.Web;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Negotiate;
using Microsoft.AspNetCore.Server.IISIntegration;
using Microsoft.Extensions.Options;

namespace SharpCrafters.Backstage.LicenseServer;

/// <summary>
/// Authenticates no caller, so that the server serves every request anonymously.
/// </summary>
public sealed class AnonymousAuthenticationHandler : AuthenticationHandler<AuthenticationSchemeOptions>
{
    public AnonymousAuthenticationHandler(
        IOptionsMonitor<AuthenticationSchemeOptions> options,
        ILoggerFactory logger,
        UrlEncoder encoder ) : base( options, logger, encoder ) { }

    public const string SchemeName = "None";

    protected override Task<AuthenticateResult> HandleAuthenticateAsync() => Task.FromResult( AuthenticateResult.NoResult() );
}