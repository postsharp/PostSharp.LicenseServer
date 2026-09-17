using Microsoft.Extensions.Options;

namespace SharpCrafters.Backstage.LicenseServer.Options;

/// <summary>
/// Validates the relationships between settings that data annotations cannot express. The legacy
/// <c>Web.config</c> documented these constraints but nothing enforced them.
/// </summary>
public sealed class LicenseServerOptionsValidator : IValidateOptions<LicenseServerOptions>
{
    public ValidateOptionsResult Validate( string? name, LicenseServerOptions options )
    {
        List<string> failures = [];

        // A lease whose renewal time is not before its end time makes the client renew on every
        // single request.
        if ( options.MinLeaseDays >= options.NewLeaseDays )
        {
            failures.Add(
                $"MinLeaseDays ({options.MinLeaseDays}) must be smaller than NewLeaseDays ({options.NewLeaseDays}), "
                + "otherwise a new lease is already due for renewal when it is granted." );
        }

        if ( options.TimeAcceleration < 0 )
        {
            failures.Add( $"TimeAcceleration ({options.TimeAcceleration}) cannot be negative." );
        }

        if ( options.AuditHmacKey != null && !IsBase64( options.AuditHmacKey ) )
        {
            failures.Add( "AuditHmacKey must be a base64-encoded string." );
        }

        return failures.Count == 0 ? ValidateOptionsResult.Success : ValidateOptionsResult.Fail( failures );
    }

    private static bool IsBase64( string value ) => Convert.TryFromBase64String( value, new byte[value.Length], out _ );
}
