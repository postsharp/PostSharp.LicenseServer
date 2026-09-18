using Microsoft.Extensions.Options;

namespace SharpCrafters.Backstage.LicenseServer.Options;

/// <summary>
/// Validates the relations between settings that a data annotation cannot express. The legacy
/// <c>Web.config</c> documented these constraints, and no code enforced them.
/// </summary>
public sealed class LicenseServerOptionsValidator : IValidateOptions<LicenseServerOptions>
{
    public ValidateOptionsResult Validate( string? name, LicenseServerOptions options )
    {
        List<string> failures = [];

        // When the renewal time of a lease is not before its end time, the client renews the lease at
        // every request.
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
