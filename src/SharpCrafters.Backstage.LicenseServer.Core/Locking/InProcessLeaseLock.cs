namespace SharpCrafters.Backstage.LicenseServer.Locking;

/// <summary>
/// Serializes the lease requests of the current process. This is the default implementation, and it
/// is correct for the supported deployment, which is one worker process per database.
/// </summary>
/// <remarks>
/// A request that waits here does not block a thread of the thread pool, and the named <c>Mutex</c>
/// that this class replaces did block one. A queued request therefore costs less, and the timeout
/// elapses less often.
/// </remarks>
public sealed class InProcessLeaseLock : ILeaseLock, IDisposable
{
    private readonly SemaphoreSlim semaphore = new( 1, 1 );

    public async ValueTask<IAsyncDisposable?> TryAcquireAsync(
        TimeSpan timeout,
        CancellationToken cancellationToken = default )
    {
        if ( !await this.semaphore.WaitAsync( timeout, cancellationToken ) )
        {
            return null;
        }

        return new Handle( this.semaphore );
    }

    public void Dispose() => this.semaphore.Dispose();

    private sealed class Handle( SemaphoreSlim semaphore ) : IAsyncDisposable
    {
        private int released;

        public ValueTask DisposeAsync()
        {
            if ( Interlocked.Exchange( ref this.released, 1 ) == 0 )
            {
                semaphore.Release();
            }

            return ValueTask.CompletedTask;
        }
    }
}
