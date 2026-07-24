namespace ServiceControl.Persistence.EFCore.Implementation;

using System.IO.Compression;
using System.Net;
using Amazon.S3;
using Amazon.S3.Model;
using ServiceControl.Infrastructure;
using ServiceControl.Persistence.EFCore.Abstractions;
using ServiceControl.Persistence.EFCore.Infrastructure;

public class S3BodyStoragePersistence : IBodyStoragePersistence
{
    const string FormatVersion = "1";

    readonly IAmazonS3 client;
    readonly string bucketName;
    readonly string keyPrefix;
    readonly int minBodySizeForCompression;

    public S3BodyStoragePersistence(EFPersisterSettings settings)
    {
        client = S3ClientFactory.Create(settings);
        bucketName = settings.S3BucketName!;
        keyPrefix = settings.S3KeyPrefix;
        minBodySizeForCompression = settings.MinBodySizeForCompression;
    }

    public async Task WriteBody(string bodyId, ReadOnlyMemory<byte> body, string contentType, CancellationToken cancellationToken = default)
    {
        var key = Key(bodyId);

        // Bodies are immutable, so an existing object is already correct.
        if (await Exists(key, cancellationToken))
        {
            return;
        }

        var compressed = body.Length >= minBodySizeForCompression ? BodyCompression.TryCompress(body) : null;

        var payload = compressed is null ? body : compressed.AsMemory();
        var request = new PutObjectRequest
        {
            BucketName = bucketName,
            Key = key,
            InputStream = new ReadOnlyStream(payload),
            ContentType = contentType
        };
        request.Metadata.Add("format-version", FormatVersion);
        request.Metadata.Add("body-size", body.Length.ToString());
        request.Metadata.Add("is-compressed", (compressed is not null).ToString());

        await client.PutObjectAsync(request, cancellationToken);
    }

    public async Task<MessageBodyFileResult?> ReadBody(string bodyId, CancellationToken cancellationToken = default)
    {
        try
        {
            var response = await client.GetObjectAsync(bucketName, Key(bodyId), cancellationToken);
            try
            {
                var metadata = response.Metadata;

                var version = metadata["format-version"];
                if (!string.IsNullOrEmpty(version) && version != FormatVersion)
                {
                    throw new InvalidOperationException($"Unsupported object format version {version} for {bodyId}.");
                }

                var bodySize = int.TryParse(metadata["body-size"], out var size) ? size : 0;
                var isCompressed = bool.TryParse(metadata["is-compressed"], out var compressed) && compressed;
                var contentType = response.Headers.ContentType ?? "application/octet-stream";
                Stream stream = isCompressed
                    ? new ExpectedLengthStream(new BrotliStream(response.ResponseStream, CompressionMode.Decompress), bodySize)
                    : response.ResponseStream;

                return new MessageBodyFileResult
                {
                    Stream = new OwnedStream(stream, response),
                    ContentType = contentType,
                    BodySize = bodySize
                };
            }
            catch
            {
                response.Dispose();
                throw;
            }
        }
        catch (AmazonS3Exception ex) when (ex.StatusCode == HttpStatusCode.NotFound)
        {
            return null;
        }
    }

    public Task DeleteBody(string bodyId, CancellationToken cancellationToken = default) =>
        client.DeleteObjectAsync(bucketName, Key(bodyId), cancellationToken);

    async Task<bool> Exists(string key, CancellationToken cancellationToken)
    {
        try
        {
            await client.GetObjectMetadataAsync(bucketName, key, cancellationToken);
            return true;
        }
        catch (AmazonS3Exception ex) when (ex.StatusCode == HttpStatusCode.NotFound)
        {
            return false;
        }
    }

    string Key(string bodyId) => $"{keyPrefix}{bodyId}";
}
