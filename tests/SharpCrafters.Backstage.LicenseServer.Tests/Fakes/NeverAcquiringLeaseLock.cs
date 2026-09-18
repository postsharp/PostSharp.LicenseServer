// Copyright (c) SharpCrafters s.r.o. See the LICENSE.md file in the root directory of this repository root for details.

using SharpCrafters.Backstage.LicenseServer.Locking;

namespace SharpCrafters.Backstage.LicenseServer.Tests.Fakes;

/// <summary>
/// Never grants the lock, so that a test can exercise the path that answers "Service overloaded."
/// </summary>
public sealed class NeverAcquiringLeaseLock : ILeaseLock
{
    public ValueTask<IAsyncDisposable?> TryAcquireAsync(
        TimeSpan timeout,
        CancellationToken cancellationToken = default )
        => ValueTask.FromResult<IAsyncDisposable?>( null );
}