namespace ServiceControl.Audit.Persistence.Sql.Core.Infrastructure;

using System.Buffers;
using System.IO.Compression;
using Azure.Storage;
using Azure.Storage.Blobs;
using Azure.Storage.Blobs.Models;
using ServiceControl.Audit.Persistence.Sql.Core.Abstractions;

public class AzureBlobBodyStoragePersistence : IBodyStoragePersistence
{
    const string FormatVersion = "1";
    readonly AuditSqlPersisterSettings settings;
    readonly BlobContainerClient blobContainerClient;

    public AzureBlobBodyStoragePersistence(AuditSqlPersisterSettings settings)
    {
        this.settings = settings;

        var blobClient = new BlobServiceClient(settings.MessageBodyStorageConnectionString);
        blobContainerClient = blobClient.GetBlobContainerClient("audit-bodies");
    }

    // Blob deletion is handled by Azure Blob Storage lifecycle management policies.
    // See: https://learn.microsoft.com/en-us/azure/storage/blobs/lifecycle-management-policy-delete
    public Task DeleteBodies(IEnumerable<string> bodyIds, CancellationToken cancellationToken = default) => Task.CompletedTask;

    public async Task<MessageBodyFileResult?> ReadBodyAsync(string bodyId, CancellationToken cancellationToken = default)
    {
        var blob = blobContainerClient.GetBlobClient(bodyId);

        try
        {
            var response = await blob.DownloadContentAsync(cancellationToken);
            var properties = response.Value;
            var metadata = properties.Details.Metadata;

            // Check format version
            if (metadata.TryGetValue("FormatVersion", out var version) && version != FormatVersion)
            {
                throw new InvalidOperationException($"Unsupported blob format version: {version}");
            }

            var contentType = metadata.TryGetValue("ContentType", out var ct) ? Uri.UnescapeDataString(ct) : "application/octet-stream";
            var bodySize = metadata.TryGetValue("BodySize", out var sizeStr) && int.TryParse(sizeStr, out var size) ? size : 0;
            var isCompressed = metadata.TryGetValue("IsCompressed", out var compressedStr) && bool.TryParse(compressedStr, out var compressed) && compressed;
            var etag = properties.Details.ETag.ToString();

            Stream stream;
            if (isCompressed)
            {
                var compressedData = properties.Content.ToMemory();
                var decompressedBuffer = new byte[bodySize];

                if (!BrotliDecoder.TryDecompress(compressedData.Span, decompressedBuffer, out var bytesWritten) || bytesWritten != bodySize)
                {
                    throw new InvalidOperationException($"Failed to decompress body for {bodyId}");
                }

                stream = new MemoryStream(decompressedBuffer, writable: false);
            }
            else
            {
                stream = properties.Content.ToStream();
            }

            return new MessageBodyFileResult
            {
                Stream = stream,
                ContentType = contentType,
                BodySize = bodySize,
                Etag = etag
            };
        }
        catch (Azure.RequestFailedException ex) when (ex.Status == 404)
        {
            return null;
        }
    }

    public async Task WriteBodyAsync(string bodyId, ReadOnlyMemory<byte> body, string contentType, CancellationToken cancellationToken = default)
    {
        var blob = blobContainerClient.GetBlobClient(bodyId);
        var shouldCompress = body.Length >= settings.MinBodySizeForCompression;

        BinaryData data;
        byte[]? rentedBuffer = null;

        try
        {
            if (shouldCompress)
            {
                var maxCompressedSize = BrotliEncoder.GetMaxCompressedLength(body.Length);
                rentedBuffer = ArrayPool<byte>.Shared.Rent(maxCompressedSize);

                if (!BrotliEncoder.TryCompress(body.Span, rentedBuffer, out var bytesWritten, quality: 1, window: 22))
                {
                    // Compression failed, fall back to uncompressed
                    data = BinaryData.FromBytes(body);
                    shouldCompress = false;
                }
                else
                {
                    data = BinaryData.FromBytes(new ReadOnlyMemory<byte>(rentedBuffer, 0, bytesWritten));
                }
            }
            else
            {
                data = BinaryData.FromBytes(body);
            }

            var options = new BlobUploadOptions
            {
                TransferValidation = new UploadTransferValidationOptions
                {
                    ChecksumAlgorithm = StorageChecksumAlgorithm.Auto
                },
                Metadata = new Dictionary<string, string>
                {
                    { "FormatVersion", FormatVersion },
                    { "ContentType", Uri.EscapeDataString(contentType) },
                    { "BodySize", body.Length.ToString() },
                    { "IsCompressed", shouldCompress.ToString() }
                }
            };

            await blob.UploadAsync(data, options, cancellationToken);
        }
        finally
        {
            if (rentedBuffer != null)
            {
                ArrayPool<byte>.Shared.Return(rentedBuffer);
            }
        }
    }
}
