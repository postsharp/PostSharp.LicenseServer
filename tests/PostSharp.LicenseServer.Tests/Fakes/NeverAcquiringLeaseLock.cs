using PostSharp.LicenseServer.Locking;

namespace PostSharp.LicenseServer.Tests.Fakes;

/// <summary>
/// Never grants the lock, so that the "service overloaded" path can be exercised.
/// </summary>
public sealed class NeverAcquiringLeaseLock : ILeaseLock
{
    public ValueTask<IAsyncDisposable?> TryAcquireAsync(
        TimeSpan timeout,
        CancellationToken cancellationToken = default )
        => ValueTask.FromResult<IAsyncDisposable?>( null );
}
