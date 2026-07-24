namespace ServiceControl.Persistence.EFCore.Implementation;

using System.Buffers;
using System.IO.Compression;

static class BodyCompression
{
    // Returns the Brotli-compressed bytes, or null when compression fails and the caller should
    // store the body uncompressed.
    public static byte[]? TryCompress(ReadOnlyMemory<byte> body)
    {
        var buffer = ArrayPool<byte>.Shared.Rent(BrotliEncoder.GetMaxCompressedLength(body.Length));
        try
        {
            return BrotliEncoder.TryCompress(body.Span, buffer, out var written, quality: 1, window: 22)
                ? buffer.AsSpan(0, written).ToArray()
                : null;
        }
        finally
        {
            ArrayPool<byte>.Shared.Return(buffer);
        }
    }

    public static byte[] Decompress(ReadOnlySpan<byte> compressed, int originalSize)
    {
        var decompressed = new byte[originalSize];

        if (!BrotliDecoder.TryDecompress(compressed, decompressed, out var written) || written != originalSize)
        {
            throw new InvalidOperationException("Failed to decompress the message body.");
        }

        return decompressed;
    }
}
