namespace SharpCrafters.Backstage.LicenseServer.Locking;

/// <summary>
/// Serializes lease requests within the current process. This is the default, and is correct for
/// the supported deployment of a single worker process per database.
/// </summary>
/// <remarks>
/// Unlike the named <c>Mutex</c> it replaces, waiting here does not block a thread pool thread, so
/// queued requests are far cheaper and the timeout fires much less often.
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
