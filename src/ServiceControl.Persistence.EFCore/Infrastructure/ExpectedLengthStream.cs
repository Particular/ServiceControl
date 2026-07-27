namespace ServiceControl.Persistence.EFCore.Infrastructure;

/// <summary>
/// Validates the byte count of a forward-only stream without buffering its contents.
/// </summary>
/// <remarks>
/// Compressed body objects record their uncompressed length in metadata. Before cloud reads were
/// streamed, eager decompression verified that length before returning the body. This wrapper
/// preserves that integrity check at end of stream. A consumer that stops reading early does not
/// perform the final length check, which is necessary to keep response streaming and cancellation.
/// </remarks>
public sealed class ExpectedLengthStream(Stream stream, long expectedLength) : Stream
{
    long bytesRead;

    public override bool CanRead => stream.CanRead;
    public override bool CanSeek => false;
    public override bool CanWrite => false;
    public override long Length => throw new NotSupportedException();

    public override long Position
    {
        get => bytesRead;
        set => throw new NotSupportedException();
    }

    public override void Flush() => throw new NotSupportedException();

    public override int Read(byte[] buffer, int offset, int count) => ValidateRead(stream.Read(buffer, offset, count));

    public override int Read(Span<byte> buffer) => ValidateRead(stream.Read(buffer));

    public override async Task<int> ReadAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken) =>
        ValidateRead(await stream.ReadAsync(buffer, offset, count, cancellationToken));

    public override async ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken = default) =>
        ValidateRead(await stream.ReadAsync(buffer, cancellationToken));

    public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();

    public override void SetLength(long value) => throw new NotSupportedException();

    public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();

    int ValidateRead(int count)
    {
        bytesRead += count;
        if (bytesRead > expectedLength || (count == 0 && bytesRead != expectedLength))
        {
            throw new InvalidOperationException("Decompressed body size does not match its metadata.");
        }

        return count;
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            stream.Dispose();
        }

        base.Dispose(disposing);
    }
}