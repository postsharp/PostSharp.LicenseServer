namespace PostSharp.LicenseServer.Locking;

/// <summary>
/// Does not serialize anything. Intended for tests that do not exercise concurrency.
/// </summary>
public sealed class NullLeaseLock : ILeaseLock
{
    private static readonly IAsyncDisposable handle = new Handle();

    public ValueTask<IAsyncDisposable?> TryAcquireAsync(
        TimeSpan timeout,
        CancellationToken cancellationToken = default )
        => ValueTask.FromResult<IAsyncDisposable?>( handle );

    private sealed class Handle : IAsyncDisposable
    {
        public ValueTask DisposeAsync() => ValueTask.CompletedTask;
    }
}
