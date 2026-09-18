// Copyright (c) SharpCrafters s.r.o. See the LICENSE.md file in the root directory of this repository root for details.

namespace SharpCrafters.Backstage.LicenseServer.Health;

/// <summary>
/// Describes the failure of a health check without disclosing why it failed.
/// </summary>
/// <remarks>
/// The message of a database exception contains the name of the server, and sometimes the whole
/// connection string. The health endpoint requires no authentication, so the server writes the
/// reason to its log. Only a development server writes the reason in the response.
/// </remarks>
internal static class HealthDescriptions
{
    public static string ForException( string summary, Exception exception, IHostEnvironment environment )
        => environment.IsDevelopment()
            ? $"{summary} {exception.Message}"
            : $"{summary} The reason is in the log of the server.";
}