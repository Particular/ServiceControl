namespace ServiceControl.Persistence.EFCore.Infrastructure;

/// <summary>
/// Couples a returned stream to another resource that must remain alive while the stream is read.
/// </summary>
/// <remarks>
/// S3 returns a response whose response stream is consumed later by ASP.NET Core, after the
/// persistence method has returned. Disposing this wrapper disposes both the exposed stream and
/// the owning response, releasing the underlying HTTP connection.
/// </remarks>
public sealed class OwnedStream(Stream stream, IDisposable owner) : Stream
{
    public override bool CanRead => stream.CanRead;
    public override bool CanSeek => stream.CanSeek;
    public override bool CanWrite => stream.CanWrite;
    public override long Length => stream.Length;

    public override long Position
    {
        get => stream.Position;
        set => stream.Position = value;
    }

    public override void Flush() => stream.Flush();

    public override Task FlushAsync(CancellationToken cancellationToken) => stream.FlushAsync(cancellationToken);

    public override int Read(byte[] buffer, int offset, int count) => stream.Read(buffer, offset, count);

    public override int Read(Span<byte> buffer) => stream.Read(buffer);

    public override Task<int> ReadAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken) =>
        stream.ReadAsync(buffer, offset, count, cancellationToken);

    public override ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken = default) =>
        stream.ReadAsync(buffer, cancellationToken);

    public override long Seek(long offset, SeekOrigin origin) => stream.Seek(offset, origin);

    public override void SetLength(long value) => stream.SetLength(value);

    public override void Write(byte[] buffer, int offset, int count) => stream.Write(buffer, offset, count);

    public override void Write(ReadOnlySpan<byte> buffer) => stream.Write(buffer);

    public override Task WriteAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken) =>
        stream.WriteAsync(buffer, offset, count, cancellationToken);

    public override ValueTask WriteAsync(ReadOnlyMemory<byte> buffer, CancellationToken cancellationToken = default) =>
        stream.WriteAsync(buffer, cancellationToken);

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            try
            {
                stream.Dispose();
            }
            finally
            {
                owner.Dispose();
            }
        }

        base.Dispose(disposing);
    }
}
