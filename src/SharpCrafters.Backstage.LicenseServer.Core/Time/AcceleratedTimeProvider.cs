// Copyright (c) SharpCrafters s.r.o. See the LICENSE.md file in the root directory of this repository root for details.

namespace SharpCrafters.Backstage.LicenseServer.Time;

/// <summary>
/// A <see cref="TimeProvider"/> whose clock runs faster than real time, so that a licensing scenario
/// that lasts several days can be simulated in minutes. Replaces the legacy <c>VirtualDateTime</c>
/// class, whose acceleration was removed from a release build by the compiler, and which no test
/// harness could therefore use.
/// </summary>
public sealed class AcceleratedTimeProvider : TimeProvider
{
    private readonly TimeProvider inner;
    private readonly DateTimeOffset origin;
    private readonly double acceleration;

    public AcceleratedTimeProvider( TimeProvider inner, double acceleration )
    {
        ArgumentNullException.ThrowIfNull( inner );

        if ( acceleration <= 0 )
        {
            throw new ArgumentOutOfRangeException( nameof(acceleration), acceleration, "Acceleration must be positive." );
        }

        this.inner = inner;
        this.acceleration = acceleration;
        this.origin = inner.GetUtcNow();
    }

    public double Acceleration => this.acceleration;

    public override DateTimeOffset GetUtcNow() => this.origin + ( ( this.inner.GetUtcNow() - this.origin ) * this.acceleration );

    public override long GetTimestamp() => this.inner.GetTimestamp();

    public override long TimestampFrequency => this.inner.TimestampFrequency;

    public override TimeZoneInfo LocalTimeZone => this.inner.LocalTimeZone;
}