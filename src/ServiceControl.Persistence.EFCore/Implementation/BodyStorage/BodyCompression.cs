namespace ServiceControl.Persistence.EFCore.Implementation.BodyStorage;

using System.Buffers;
using System.IO.Compression;

/// <summary>
/// Compresses message bodies on their way into cloud storage.
/// </summary>
/// <remarks>
/// Brotli ships in the BCL, so it costs no extra dependency, and at quality 1 it compresses about as
/// quickly as gzip or deflate while producing a smaller payload. The quality is pinned deliberately
/// low because this runs on the ingestion path, where throughput matters more than the last few
/// percent of ratio; window 22 is the .NET default. <see cref="FileSystemBodyStoragePersistence"/>
/// produces the same format through BrotliStream, which maps CompressionLevel.Fastest onto exactly
/// these parameters, so the two paths stay interchangeable without sharing this buffer-based helper.
/// Decompression is not here on purpose: every read path streams, so nothing needs the whole body in
/// memory at once.
/// </remarks>
static class BodyCompression
{
    /// <summary>
    /// Returns the Brotli-compressed bytes, or null when compression fails and the caller should
    /// store the body uncompressed.
    /// </summary>
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
}
