namespace ServiceControl.Persistence.EFCore.Implementation;

using System.Buffers;
using System.IO.Compression;
using Azure;
using Azure.Storage.Blobs;
using Azure.Storage.Blobs.Models;
using ServiceControl.Persistence.EFCore.Abstractions;
using ServiceControl.Persistence.EFCore.Infrastructure;

public class AzureBlobBodyStoragePersistence : IBodyStoragePersistence
{
    const string FormatVersion = "1";

    readonly BlobContainerClient container;
    readonly int minBodySizeForCompression;

    public AzureBlobBodyStoragePersistence(EFPersisterSettings settings)
    {
        container = AzureBlobClientFactory.CreateContainerClient(settings);
        minBodySizeForCompression = settings.MinBodySizeForCompression;
    }

    public async Task WriteBody(string bodyId, ReadOnlyMemory<byte> body, string contentType, CancellationToken cancellationToken = default)
    {
        var blob = container.GetBlobClient(bodyId);
        var shouldCompress = body.Length >= minBodySizeForCompression;

        BinaryData data;
        byte[]? rentedBuffer = null;
        try
        {
            if (shouldCompress)
            {
                rentedBuffer = ArrayPool<byte>.Shared.Rent(BrotliEncoder.GetMaxCompressedLength(body.Length));

                if (BrotliEncoder.TryCompress(body.Span, rentedBuffer, out var bytesWritten, quality: 1, window: 22))
                {
                    data = BinaryData.FromBytes(new ReadOnlyMemory<byte>(rentedBuffer, 0, bytesWritten));
                }
                else
                {
                    data = BinaryData.FromBytes(body);
                    shouldCompress = false;
                }
            }
            else
            {
                data = BinaryData.FromBytes(body);
            }

            var options = new BlobUploadOptions
            {
                // Bodies are immutable, so create the blob only if it does not already exist.
                Conditions = new BlobRequestConditions { IfNoneMatch = ETag.All },
                Metadata = new Dictionary<string, string>
                {
                    ["FormatVersion"] = FormatVersion,
                    ["ContentType"] = Uri.EscapeDataString(contentType),
                    ["BodySize"] = body.Length.ToString(),
                    ["IsCompressed"] = shouldCompress.ToString()
                }
            };

            try
            {
                await blob.UploadAsync(data, options, cancellationToken);
            }
            catch (RequestFailedException ex) when (ex.Status is 409 or 412)
            {
                // A blob for this immutable body already exists.
            }
        }
        finally
        {
            if (rentedBuffer != null)
            {
                ArrayPool<byte>.Shared.Return(rentedBuffer);
            }
        }
    }

    public async Task<MessageBodyFileResult?> ReadBody(string bodyId, CancellationToken cancellationToken = default)
    {
        var blob = container.GetBlobClient(bodyId);

        try
        {
            var content = (await blob.DownloadContentAsync(cancellationToken)).Value;
            var metadata = content.Details.Metadata;

            if (metadata.TryGetValue("FormatVersion", out var version) && version != FormatVersion)
            {
                throw new InvalidOperationException($"Unsupported blob format version {version} for {bodyId}.");
            }

            var contentType = metadata.TryGetValue("ContentType", out var ct) ? Uri.UnescapeDataString(ct) : "application/octet-stream";
            var bodySize = metadata.TryGetValue("BodySize", out var sizeText) && int.TryParse(sizeText, out var size) ? size : 0;
            var isCompressed = metadata.TryGetValue("IsCompressed", out var compressedText) && bool.TryParse(compressedText, out var compressed) && compressed;

            Stream stream;
            if (isCompressed)
            {
                var decompressed = new byte[bodySize];
                if (!BrotliDecoder.TryDecompress(content.Content.ToMemory().Span, decompressed, out var written) || written != bodySize)
                {
                    throw new InvalidOperationException($"Failed to decompress body for {bodyId}.");
                }

                stream = new MemoryStream(decompressed, writable: false);
            }
            else
            {
                stream = content.Content.ToStream();
            }

            return new MessageBodyFileResult
            {
                Stream = stream,
                ContentType = contentType,
                BodySize = bodySize
            };
        }
        catch (RequestFailedException ex) when (ex.Status == 404)
        {
            return null;
        }
    }

    public Task DeleteBody(string bodyId, CancellationToken cancellationToken = default) =>
        container.GetBlobClient(bodyId).DeleteIfExistsAsync(cancellationToken: cancellationToken);
}
