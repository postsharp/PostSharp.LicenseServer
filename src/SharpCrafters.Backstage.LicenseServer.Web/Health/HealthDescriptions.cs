namespace SharpCrafters.Backstage.LicenseServer.Health;

/// <summary>
/// Describes the failure of a health check without disclosing why it failed.
/// </summary>
/// <remarks>
/// The message of a database exception carries the name of the server, and sometimes the whole
/// connection string. The health endpoint is anonymous, so the reason goes to the log of the server
/// and only a development server puts it in the response.
/// </remarks>
internal static class HealthDescriptions
{
    public static string ForException( string summary, Exception exception, IHostEnvironment environment )
        => environment.IsDevelopment()
            ? $"{summary} {exception.Message}"
            : $"{summary} The reason is in the log of the server.";
}
