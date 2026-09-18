// Copyright (c) SharpCrafters s.r.o. See the LICENSE.md file in the root directory of this repository root for details.

using Microsoft.EntityFrameworkCore;
using SharpCrafters.Backstage.LicenseServer.Data;
using SharpCrafters.Backstage.LicenseServer.Licensing;

namespace SharpCrafters.Backstage.LicenseServer.Services;

/// <summary>
/// How many licenses the server holds, and why the ones it cannot serve are unusable.
/// </summary>
public sealed record LicenseAvailability
{
    /// <summary>
    /// Gets the number of licenses that can serve a lease now.
    /// </summary>
    public required int Available { get; init; }

    /// <summary>
    /// Gets the number of licenses an administrator has disabled.
    /// </summary>
    public required int Disabled { get; init; }

    /// <summary>
    /// Gets the number of licenses whose key the server cannot parse, whose type a license server
    /// may not serve, or that require a newer licensing library than this server contains.
    /// </summary>
    public required int Invalid { get; init; }

    /// <summary>
    /// Gets the number of licenses whose validity has ended.
    /// </summary>
    public required int Expired { get; init; }

    /// <summary>
    /// Gets the number of licenses that are full and whose grace period has ended or is full as
    /// well.
    /// </summary>
    public required int Exhausted { get; init; }

    public int Total => this.Available + this.Disabled + this.Invalid + this.Expired + this.Exhausted;

    public bool CanServeLease => this.Available > 0;

    /// <summary>
    /// Returns one sentence that says whether a lease can be served, and why not when it cannot.
    /// </summary>
    public string Describe()
    {
        if ( this.CanServeLease )
        {
            return $"{this.Available} of {this.Total} license(s) can serve a lease.";
        }

        if ( this.Total == 0 )
        {
            return "No license is registered.";
        }

        List<string> reasons = [];

        Add( this.Exhausted, "at capacity" );
        Add( this.Expired, "expired" );
        Add( this.Invalid, "invalid" );
        Add( this.Disabled, "disabled" );

        return $"No license can serve a lease: {string.Join( ", ", reasons )}.";

        void Add( int count, string reason )
        {
            if ( count > 0 )
            {
                reasons.Add( $"{count} {reason}" );
            }
        }
    }
}