using PostSharp.Platform.NetStandard20;
using PostSharp.Platform.Neutral;

namespace SharpCrafters.Backstage.LicenseServer.Licensing;

/// <summary>
/// Initializes the PostSharp SDK platform services. Must run once before any license key is parsed.
/// </summary>
/// <remarks>
/// The legacy code did this in the static constructor of <c>ParsedLicenseManager</c> and used
/// <c>NetFrameworkDefaultSystemServices</c>, which does not exist outside .NET Framework.
/// </remarks>
public static class PostSharpPlatform
{
    private static readonly Lock sync = new();
    private static bool initialized;

    public static void EnsureInitialized()
    {
        if ( initialized )
        {
            return;
        }

        lock ( sync )
        {
            if ( initialized )
            {
                return;
            }

            CommonDefaultSystemServices.Initialize();
            NetCoreAppDefaultSystemServices.Initialize();
            initialized = true;
        }
    }
}
