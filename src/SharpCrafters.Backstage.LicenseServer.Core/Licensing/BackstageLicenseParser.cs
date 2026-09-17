using SharpCrafters.Backstage.Licensing;
using SharpCrafters.Backstage.Licensing.Licenses;
using SharpCrafters.Backstage.Licensing.Registration;

namespace SharpCrafters.Backstage.LicenseServer.Licensing;

/// <summary>
/// Parses and validates license keys with SharpCrafters.Backstage. This is the only class that
/// touches the licensing types of that package; everything else works with <see cref="LicenseInfo"/>.
/// </summary>
/// <param name="authorities">
/// The authority of the keys whose signature is accepted. Production keys by default; a test signs
/// with an authority of its own.
/// </param>
public sealed class BackstageLicenseParser( ILicensingAuthorityProvider? authorities = null ) : ILicenseParser
{
    /// <summary>
    /// The percentage of additional seats tolerated during the grace period, when the license key
    /// does not carry one. Backstage leaves the field null, so the default is applied here; it is the
    /// one PostSharp applied, so a license key that was served before is served the same way.
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

        // The fields are checked before the signature, as the consumption path of a client does: a
        // key that carries a must-understand field this version does not know cannot be served,
        // whether or not the signature is valid.
        if ( !data.ValidateFields( out _ ) )
        {
            return null;
        }

        // Verification asks the authority provider for the key the signature was created with, and a
        // provider throws when it holds no key of that identifier. A license key is pasted into a web
        // form by an administrator, so one naming an identifier nobody ever issued has to be reported
        // as an invalid key rather than escape as an unhandled exception.
        if ( data.RequiresSignature()
             && (data.SignatureKeyId == null || !this.authorities.KeyIds.Contains( data.SignatureKeyId.Value )) )
        {
            return null;
        }

        if ( !data.TryVerifySignature( this.authorities, out _ ) )
        {
            return null;
        }

        // The registration properties are where the rules that derive a value from several fields
        // live -- the eligibility of a key that predates the LicenseServerEligible field, the
        // minimal PostSharp version of a key that predates MinPostSharpVersion, the normalization of
        // the products that were renamed. Reading the fields directly would reimplement them.
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
    /// Removes the whitespace that a license key picks up when it is pasted out of an email.
    /// </summary>
    /// <remarks>
    /// A license key is Base32 after its identifier and a hyphen, so no character it can legitimately
    /// contain is whitespace. Backstage has no equivalent method: it trims a license string and
    /// nothing more, because its keys arrive from a command line or a configuration file rather than
    /// from a web form.
    /// </remarks>
    public string CleanLicenseString( string licenseKey )
        => new( licenseKey.Where( c => !char.IsWhiteSpace( c ) ).ToArray() );
}
