using PostSharp.Sdk.Extensibility.Licensing;
using ParsedLicense = PostSharp.Sdk.Extensibility.Licensing.License;

namespace SharpCrafters.Backstage.LicenseServer.Licensing;

/// <summary>
/// Parses license keys with the PostSharp SDK. This is the only class that touches the SDK's
/// licensing types; everything else works with <see cref="LicenseInfo"/>.
/// </summary>
public sealed class PostSharpLicenseParser : ILicenseParser
{
    public LicenseInfo? TryParse( string licenseKey )
    {
        if ( string.IsNullOrWhiteSpace( licenseKey ) )
        {
            return null;
        }

        ParsedLicense? parsedLicense = ParsedLicense.Deserialize( licenseKey );

        if ( parsedLicense == null || !parsedLicense.Validate( null, out _ ) )
        {
            return null;
        }

        return new LicenseInfo
        {
            LicenseId = parsedLicense.LicenseId,
            Product = parsedLicense.Product.ToString(),
            LicenseType = parsedLicense.LicenseType.ToString(),
            UserNumber = parsedLicense.UserNumber,
            ValidTo = parsedLicense.ValidTo,
            SubscriptionEndDate = parsedLicense.SubscriptionEndDate,
            MinPostSharpVersion = parsedLicense.MinPostSharpVersion,
            GraceDays = parsedLicense.GetGraceDaysOrDefault(),
            GracePercent = parsedLicense.GetGracePercentOrDefault(),
            IsLicenseServerEligible = parsedLicense.IsLicenseServerEligible(),
            LicenseTypeName = parsedLicense.GetLicenseTypeName(),
            ProductName = parsedLicense.GetProductName()
        };
    }

    public string CleanLicenseString( string licenseKey ) => ParsedLicense.CleanLicenseString( licenseKey );
}
