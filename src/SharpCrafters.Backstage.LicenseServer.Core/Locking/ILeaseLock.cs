namespace SharpCrafters.Backstage.LicenseServer.Locking;

/// <summary>
/// Serializes lease requests, so that two concurrent requests cannot both decide that the last
/// remaining seat is free. Replaces the machine-wide named <c>Mutex</c> of the legacy
/// <c>Lease.ashx</c> handler, which was Windows-only and blocked a thread pool thread.
/// </summary>
public interface ILeaseLock
{
    /// <summary>
    /// Acquires the lock, waiting at most <paramref name="timeout"/>.
    /// </summary>
    /// <returns>
    /// A handle that releases the lock when disposed, or <c>null</c> when the timeout elapsed, in
    /// which case the caller answers HTTP 503.
    /// </returns>
    ValueTask<IAsyncDisposable?> TryAcquireAsync( TimeSpan timeout, CancellationToken cancellationToken = default );
}
