namespace SharpCrafters.Backstage.LicenseServer.Locking;

/// <summary>
/// Serializes nothing. This implementation exists for the tests that do not exercise concurrency.
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
