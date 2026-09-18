using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;

namespace SharpCrafters.Backstage.LicenseServer.Tests.Infrastructure;

/// <summary>
/// Makes the response body of the test host refuse a synchronous write, as Kestrel does.
/// </summary>
/// <remarks>
/// <see cref="Microsoft.AspNetCore.TestHost.TestServer"/> accepts a synchronous write whatever the
/// value of its <c>AllowSynchronousIO</c> property. An endpoint that writes synchronously therefore
/// passes every test, and then fails against a real server with the message "Synchronous operations
/// are disallowed". This filter wraps the body in a stream that raises the same exception.
/// </remarks>
public sealed class AsyncOnlyResponseBody : IStartupFilter
{
    /// <summary>
    /// Gets or sets a value indicating whether the stream refuses a synchronous write. The value is
    /// false by default, so that a test that does not read the response body is not affected.
    /// </summary>
    public bool IsEnabled { get; set; }

    public Action<IApplicationBuilder> Configure( Action<IApplicationBuilder> next )
        => builder =>
        {
            builder.Use(
                async ( context, nextMiddleware ) =>
                {
                    if ( !this.IsEnabled )
                    {
                        await nextMiddleware( context );

                        return;
                    }

                    Stream original = context.Response.Body;
                    context.Response.Body = new AsyncOnlyStream( original );

                    try
                    {
                        await nextMiddleware( context );
                    }
                    finally
                    {
                        context.Response.Body = original;
                    }
                } );

            next( builder );
        };

    /// <summary>
    /// Forwards every asynchronous write, and raises an exception at every synchronous write, with
    /// the message that Kestrel uses.
    /// </summary>
    private sealed class AsyncOnlyStream : Stream
    {
        private readonly Stream inner;

        public AsyncOnlyStream( Stream inner )
        {
            this.inner = inner;
        }

        private const string message =
            "Synchronous operations are disallowed. Call WriteAsync or set AllowSynchronousIO to true instead.";

        public override bool CanRead => false;

        public override bool CanSeek => false;

        public override bool CanWrite => this.inner.CanWrite;

        public override long Length => throw new NotSupportedException();

        public override long Position
        {
            get => throw new NotSupportedException();
            set => throw new NotSupportedException();
        }

        public override void Write( byte[] buffer, int offset, int count ) => throw new InvalidOperationException( message );

        public override void Write( ReadOnlySpan<byte> buffer ) => throw new InvalidOperationException( message );

        public override void WriteByte( byte value ) => throw new InvalidOperationException( message );

        public override void Flush() => throw new InvalidOperationException( message );

        public override Task WriteAsync( byte[] buffer, int offset, int count, CancellationToken cancellationToken )
            => this.inner.WriteAsync( buffer, offset, count, cancellationToken );

        public override ValueTask WriteAsync( ReadOnlyMemory<byte> buffer, CancellationToken cancellationToken = default )
            => this.inner.WriteAsync( buffer, cancellationToken );

        public override Task FlushAsync( CancellationToken cancellationToken ) => this.inner.FlushAsync( cancellationToken );

        public override int Read( byte[] buffer, int offset, int count ) => throw new NotSupportedException();

        public override long Seek( long offset, SeekOrigin origin ) => throw new NotSupportedException();

        public override void SetLength( long value ) => throw new NotSupportedException();
    }
}
