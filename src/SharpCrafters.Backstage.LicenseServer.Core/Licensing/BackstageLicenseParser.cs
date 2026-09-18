using SharpCrafters.Backstage.Licensing;
using SharpCrafters.Backstage.Licensing.Licenses;
using SharpCrafters.Backstage.Licensing.Registration;

namespace SharpCrafters.Backstage.LicenseServer.Licensing;

/// <summary>
/// Parses and validates license keys with SharpCrafters.Backstage. This class is the only one that
/// uses the licensing types of that package. The rest of the application uses
/// <see cref="LicenseInfo"/>.
/// </summary>
/// <param name="authorities">
/// The authorities whose signature the parser accepts. The default authorities are the production
/// ones. A test signs with an authority of its own.
/// </param>
public sealed class BackstageLicenseParser( ILicensingAuthorityProvider? authorities = null ) : ILicenseParser
{
    /// <summary>
    /// The percentage of additional seats allowed during the grace period, when the license key
    /// contains no percentage. Backstage leaves the field null, so this class applies a default
    /// value. The value is the one PostSharp applied, so a license key served by an earlier version
    /// of this server is served in the same way.
    /// </summary>
    private const int defaultGracePercent = 30;

    private readonly ILicensingAuthorityProvider authorities =
        authorities ?? new ProductionLicensingAuthorityProvider();

    public LicenseInfo? TryParse( string licenseKey )
    {
        if ( string.IsNullOrWhiteSpace( licenseKey ) )
        {
            return null;
        }

        if ( !LicenseKeyData.TryDeserialize( licenseKey, out LicenseKeyData? data, out _ ) )
        {
            return null;
        }

        // The fields are validated before the signature, as a client validates them. A key that
        // contains a required field that this version does not know cannot be served, whether or not
        // its signature is valid.
        if ( !data.ValidateFields( out _ ) )
        {
            return null;
        }

        // The verification asks the authority provider for the key that created the signature, and
        // the provider raises an exception when it holds no key of that identifier. An administrator
        // pastes a license key into a web form, so a key that names an identifier that was never
        // issued must be reported as an invalid key and not as an unhandled exception. This code
        // works around postsharp-ops/SharpCrafters.Backstage#2. It can be removed when
        // TryVerifySignature returns false for an unknown identifier.
        if ( data.RequiresSignature()
             && (data.SignatureKeyId == null || !this.authorities.KeyIds.Contains( data.SignatureKeyId.Value )) )
        {
            return null;
        }

        if ( !data.TryVerifySignature( this.authorities, out _ ) )
        {
            return null;
        }

        // The registration properties contain the rules that derive a value from several fields: the
        // eligibility of a key that is older than the LicenseServerEligible field, the minimal
        // PostSharp version of a key that is older than MinPostSharpVersion, and the normalization
        // of the products that were renamed. Reading the fields directly would duplicate these
        // rules.
        LicenseRegistrationProperties properties = data.ToLicenseRegistrationProperties(
            LicenseServerProductCatalog.Instance,
            licenseKey );

        return new LicenseInfo
        {
            LicenseId = data.LicenseId,
            Product = ProductCodes.ForStorage( properties.Product ),
            LicenseType = properties.LicenseType.ToString(),
            UserNumber = data.UserNumber,
            ValidTo = properties.ValidTo,
            SubscriptionEndDate = properties.SubscriptionEndDate,
            MinPostSharpVersion = properties.MinPostSharpVersion,
            GraceDays = data.GraceDays,
            GracePercent = data.GracePercent ?? defaultGracePercent,
            IsLicenseServerEligible = properties.LicenseServerEligible,
            LicenseTypeName = properties.LicenseType.GetLicenseTypeName(),
            ProductName = LicenseServerProductCatalog.Instance.GetDisplayName( properties.Product )
        };
    }

    /// <summary>
    /// Removes the whitespace that a license key receives when it is copied from an e-mail.
    /// </summary>
    /// <remarks>
    /// After its identifier and a hyphen, a license key contains only Base32 characters, so it
    /// contains no whitespace. Backstage has no equivalent method. It trims a license string and
    /// does nothing else, because its keys come from a command line or from a configuration file,
    /// and not from a web form.
    /// </remarks>
    public string CleanLicenseString( string licenseKey )
        => new( licenseKey.Where( c => !char.IsWhiteSpace( c ) ).ToArray() );
}
