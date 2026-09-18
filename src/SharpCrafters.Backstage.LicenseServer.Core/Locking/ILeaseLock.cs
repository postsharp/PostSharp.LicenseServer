namespace SharpCrafters.Backstage.LicenseServer.Locking;

/// <summary>
/// Serializes the lease requests, so that two concurrent requests cannot both take the last free
/// seat. Replaces the named <c>Mutex</c> of the legacy <c>Lease.ashx</c> handler, which covered the
/// whole machine, ran only on Windows, and blocked a thread of the thread pool.
/// </summary>
public interface ILeaseLock
{
    /// <summary>
    /// Acquires the lock, waiting at most <paramref name="timeout"/>.
    /// </summary>
    /// <returns>
    /// A handle that releases the lock when it is disposed, or <c>null</c> when the timeout elapsed.
    /// The caller then answers with the status 503.
    /// </returns>
    ValueTask<IAsyncDisposable?> TryAcquireAsync( TimeSpan timeout, CancellationToken cancellationToken = default );
}
