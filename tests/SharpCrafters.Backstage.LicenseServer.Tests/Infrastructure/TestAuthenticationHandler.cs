// Copyright (c) SharpCrafters s.r.o. See the LICENSE.md file in the root directory of this repository root for details.

using System.Security.Claims;
using System.Text.Encodings.Web;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using SharpCrafters.Backstage.LicenseServer.Data;
using SharpCrafters.Backstage.LicenseServer.Email;
using SharpCrafters.Backstage.LicenseServer.Licensing;
using SharpCrafters.Backstage.LicenseServer.Locking;
using SharpCrafters.Backstage.LicenseServer.Options;
using SharpCrafters.Backstage.LicenseServer.Tests.Fakes;
using SharpCrafters.Common;
using System.Globalization;

namespace SharpCrafters.Backstage.LicenseServer.Tests.Infrastructure;

/// <summary>
/// Authenticates every request as the same Windows-style identity, so the tests exercise the
/// authenticated path without a domain controller.
/// </summary>
public sealed class TestAuthenticationHandler : AuthenticationHandler<AuthenticationSchemeOptions>
{
    public TestAuthenticationHandler(
        IOptionsMonitor<AuthenticationSchemeOptions> options,
        ILoggerFactory logger,
        UrlEncoder encoder ) : base( options, logger, encoder ) { }

    public const string SchemeName = "Test";

    /// <summary>
    /// The header a test sets to be served anonymously instead.
    /// </summary>
    public const string AnonymousHeader = "X-Test-Anonymous";

    protected override Task<AuthenticateResult> HandleAuthenticateAsync()
    {
        if ( this.Request.Headers.ContainsKey( AnonymousHeader ) )
        {
            return Task.FromResult( AuthenticateResult.NoResult() );
        }

        ClaimsIdentity identity = new(
            [new Claim( ClaimTypes.Name, "DOMAIN\\tester" )],
            SchemeName );

        return Task.FromResult( AuthenticateResult.Success( new AuthenticationTicket( new ClaimsPrincipal( identity ), SchemeName ) ) );
    }
}