namespace ServiceControl.Audit.Persistence.MongoDB.BodyStorage
{
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Linq;
    using System.Threading;
    using System.Threading.Channels;
    using System.Threading.Tasks;
    using Auditing.BodyStorage;
    using Azure;
    using Azure.Storage.Blobs;
    using Azure.Storage.Blobs.Models;
    using Microsoft.Extensions.Logging;

    /// <summary>
    /// Body storage implementation that uses Azure Blob Storage to store message bodies. Each body is stored as a separate blob, with metadata for content type and size.
    /// The implementation includes retry logic for transient failures when uploading blobs, and uses a batched writer to optimize performance when storing large volumes of messages.
    /// </summary>
    class AzureBlobBodyStorage(
        Channel<BodyWriteItem> channel,
        MongoSettings settings,
        ILogger<AzureBlobBodyStorage> logger)
        : BatchedBodyStorageWriter<BodyWriteItem>(channel, settings, logger), IBodyStorage, IBodyWriter
    {
        const int MaxRetries = 3;
        readonly BlobContainerClient containerClient = new(settings.BlobConnectionString, settings.BlobContainerName);

        protected override string WriterName => "Azure Blob body storage writer";

        // Initialization

        public async Task Initialize(CancellationToken cancellationToken)
        {
            _ = await containerClient.CreateIfNotExistsAsync(cancellationToken: cancellationToken).ConfigureAwait(false);
            logger.LogInformation("Azure Blob body storage initialized. Container: {ContainerName}", containerClient.Name);
        }

        // IBodyWriter

        public bool IsEnabled => true;

        public async ValueTask WriteAsync(string id, string contentType, ReadOnlyMemory<byte> body, DateTime expiresAt, CancellationToken cancellationToken)
        {
            await WriteToChannelAsync(new BodyWriteItem
            {
                Id = id,
                ContentType = contentType,
                BodySize = body.Length,
                Body = body.ToArray(),
                ExpiresAt = expiresAt
            }, cancellationToken).ConfigureAwait(false);
        }

        // IBodyStorage

        public Task Store(string bodyId, string contentType, int bodySize, Stream bodyStream, CancellationToken cancellationToken)
            => Task.CompletedTask;

        public async Task<StreamResult> TryFetch(string bodyId, CancellationToken cancellationToken)
        {
            var blobClient = containerClient.GetBlobClient(bodyId);

            try
            {
                var response = await blobClient.DownloadStreamingAsync(cancellationToken: cancellationToken).ConfigureAwait(false);
                var details = response.Value.Details;

                var bodySize = 0;
                if (details.Metadata.TryGetValue("bodySize", out var bodySizeStr))
                {
                    _ = int.TryParse(bodySizeStr, out bodySize);
                }

                return new StreamResult
                {
                    HasResult = true,
                    Stream = response.Value.Content,
                    ContentType = details.ContentType ?? "text/plain",
                    BodySize = bodySize,
                    Etag = details.ETag.ToString()
                };
            }
            catch (RequestFailedException ex) when (ex.Status == 404)
            {
                return new StreamResult { HasResult = false };
            }
        }

        // BatchedBodyStorageWriter

        protected override async Task FlushBatchAsync(List<BodyWriteItem> batch, CancellationToken cancellationToken)
        {
            var uploadTasks = batch.Select(entry => UploadBlobWithRetry(entry, cancellationToken));
            await Task.WhenAll(uploadTasks).ConfigureAwait(false);
        }

        async Task UploadBlobWithRetry(BodyWriteItem entry, CancellationToken cancellationToken)
        {
            var blobClient = containerClient.GetBlobClient(entry.Id);

            for (var attempt = 1; attempt <= MaxRetries; attempt++)
            {
                try
                {
                    using var stream = new MemoryStream(entry.Body);
                    var options = new BlobUploadOptions
                    {
                        HttpHeaders = new BlobHttpHeaders { ContentType = entry.ContentType.Trim() },
                        Metadata = new Dictionary<string, string>
                        {
                            ["messageId"] = entry.Id.Trim(),
                            ["bodySize"] = entry.BodySize.ToString(),
                            ["mongoExpiresAt"] = entry.ExpiresAt.ToString("O")
                        }
                    };
                    _ = await blobClient.UploadAsync(stream, options, cancellationToken).ConfigureAwait(false);
                    return;
                }
                catch (Exception ex) when (attempt < MaxRetries && !cancellationToken.IsCancellationRequested)
                {
                    var delay = TimeSpan.FromSeconds(Math.Pow(2, attempt - 1));
                    logger.LogWarning(ex, "Failed to upload blob {BlobId} (attempt {Attempt}/{MaxRetries}), retrying in {Delay}s",
                        entry.Id, attempt, MaxRetries, delay.TotalSeconds);
                    await Task.Delay(delay, cancellationToken).ConfigureAwait(false);
                }
                catch (Exception ex)
                {
                    logger.LogError(ex, "Failed to upload blob {BlobId} after {MaxRetries} attempts", entry.Id, MaxRetries);
                }
            }
        }
    }
}
