namespace ServiceControl.Persistence.EFCore.Implementation;

using System.IO.Compression;
using System.Net;
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

        var compressed = body.Length >= minBodySizeForCompression ? BodyCompression.TryCompress(body) : null;
        var data = compressed is null ? BinaryData.FromBytes(body) : BinaryData.FromBytes(compressed);

        var options = new BlobUploadOptions
        {
            // Bodies are immutable, so create the blob only if it does not already exist.
            Conditions = new BlobRequestConditions { IfNoneMatch = ETag.All },
            Metadata = new Dictionary<string, string>
            {
                ["FormatVersion"] = FormatVersion,
                ["ContentType"] = Uri.EscapeDataString(contentType),
                ["BodySize"] = body.Length.ToString(),
                ["IsCompressed"] = (compressed is not null).ToString()
            }
        };

        try
        {
            await blob.UploadAsync(data, options, cancellationToken);
        }
        catch (RequestFailedException ex) when (ex.Status is (int)HttpStatusCode.Conflict or (int)HttpStatusCode.PreconditionFailed)
        {
            // A blob for this immutable body already exists.
        }
    }

    public async Task<MessageBodyFileResult?> ReadBody(string bodyId, CancellationToken cancellationToken = default)
    {
        var blob = container.GetBlobClient(bodyId);

        try
        {
            var content = (await blob.DownloadStreamingAsync(cancellationToken: cancellationToken)).Value;
            try
            {
                var metadata = content.Details.Metadata;

                if (metadata.TryGetValue("FormatVersion", out var version) && version != FormatVersion)
                {
                    throw new InvalidOperationException($"Unsupported blob format version {version} for {bodyId}.");
                }

                var contentType = metadata.TryGetValue("ContentType", out var ct) ? Uri.UnescapeDataString(ct) : "application/octet-stream";
                var bodySize = metadata.TryGetValue("BodySize", out var sizeText) && int.TryParse(sizeText, out var size) ? size : 0;
                var isCompressed = metadata.TryGetValue("IsCompressed", out var compressedText) && bool.TryParse(compressedText, out var compressed) && compressed;
                Stream stream = isCompressed
                    ? new ExpectedLengthStream(new BrotliStream(content.Content, CompressionMode.Decompress), bodySize)
                    : content.Content;

                return new MessageBodyFileResult
                {
                    Stream = stream,
                    ContentType = contentType,
                    BodySize = bodySize
                };
            }
            catch
            {
                content.Dispose();
                throw;
            }
        }
        catch (RequestFailedException ex) when (ex.Status == (int)HttpStatusCode.NotFound)
        {
            return null;
        }
    }

    public Task DeleteBody(string bodyId, CancellationToken cancellationToken = default) =>
        container.GetBlobClient(bodyId).DeleteIfExistsAsync(cancellationToken: cancellationToken);
}
