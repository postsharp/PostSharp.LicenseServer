namespace PostSharp.LicenseServer.Time;

/// <summary>
/// A <see cref="TimeProvider"/> that makes time pass faster than it really does, so that a
/// multi-day licensing scenario can be simulated in minutes. Replaces the legacy
/// <c>VirtualDateTime</c> class, whose acceleration was compiled out of RELEASE builds and was
/// therefore unusable by any test harness.
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

    public override DateTimeOffset GetUtcNow()
        => this.origin + ((this.inner.GetUtcNow() - this.origin) * this.acceleration);

    public override long GetTimestamp() => this.inner.GetTimestamp();

    public override long TimestampFrequency => this.inner.TimestampFrequency;

    public override TimeZoneInfo LocalTimeZone => this.inner.LocalTimeZone;
}
