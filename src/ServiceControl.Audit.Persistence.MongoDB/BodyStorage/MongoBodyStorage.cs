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
    using Collections;
    using Documents;
    using global::MongoDB.Driver;
    using Microsoft.Extensions.Logging;

    class MongoBodyStorage(
        Channel<BodyWriteItem> channel,
        IMongoClientProvider clientProvider,
        MongoSettings settings,
        ILogger<MongoBodyStorage> logger)
        : BatchedBodyStorageWriter<BodyWriteItem>(channel, settings, logger), IBodyStorage, IBodyWriter
    {
        const int MaxRetries = 3;

        protected override string WriterName => "Mongo body storage writer";

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
                TextBody = TryGetUtf8String(body),
                ExpiresAt = expiresAt
            }, cancellationToken).ConfigureAwait(false);
        }

        // IBodyStorage

        public Task Store(string bodyId, string contentType, int bodySize, Stream bodyStream, CancellationToken cancellationToken)
            => Task.CompletedTask;

        public async Task<StreamResult> TryFetch(string bodyId, CancellationToken cancellationToken)
        {
            var collection = clientProvider.Database
                .GetCollection<MessageBodyDocument>(CollectionNames.MessageBodies);

            var filter = Builders<MessageBodyDocument>.Filter.Eq(d => d.Id, bodyId);
            var document = await collection.Find(filter)
                .FirstOrDefaultAsync(cancellationToken)
                .ConfigureAwait(false);

            if (document == null)
            {
                return new StreamResult { HasResult = false };
            }

            byte[] bodyBytes;
            if (document.TextBody != null)
            {
                bodyBytes = System.Text.Encoding.UTF8.GetBytes(document.TextBody);
            }
            else if (document.BinaryBody != null)
            {
                bodyBytes = document.BinaryBody;
            }
            else
            {
                return new StreamResult { HasResult = false };
            }

            return new StreamResult
            {
                HasResult = true,
                Stream = new MemoryStream(bodyBytes),
                ContentType = document.ContentType ?? "text/plain",
                BodySize = document.BodySize,
                Etag = bodyId
            };
        }

        // BatchedBodyStorageWriter

        protected override async Task FlushBatchAsync(List<BodyWriteItem> batch, CancellationToken cancellationToken)
        {
            var collection = clientProvider.Database
                .GetCollection<MessageBodyDocument>(CollectionNames.MessageBodies);

            var writes = batch.Select(entry =>
                new ReplaceOneModel<MessageBodyDocument>(
                    Builders<MessageBodyDocument>.Filter.Eq(d => d.Id, entry.Id),
                    ToDocument(entry))
                { IsUpsert = true })
                .ToList();

            for (var attempt = 1; attempt <= MaxRetries; attempt++)
            {
                try
                {
                    _ = await collection.BulkWriteAsync(writes, new BulkWriteOptions { IsOrdered = false }, cancellationToken).ConfigureAwait(false);
                    return;
                }
                catch (Exception ex) when (attempt < MaxRetries && !cancellationToken.IsCancellationRequested)
                {
                    var delay = TimeSpan.FromSeconds(Math.Pow(2, attempt - 1));
                    logger.LogWarning(ex, "Failed to write {Count} body entries (attempt {Attempt}/{MaxRetries}), retrying in {Delay}s",
                        batch.Count, attempt, MaxRetries, delay.TotalSeconds);
                    await Task.Delay(delay, cancellationToken).ConfigureAwait(false);
                }
                catch (Exception ex)
                {
                    logger.LogError(ex, "Failed to write {Count} body entries after {MaxRetries} attempts", batch.Count, MaxRetries);
                }
            }
        }

        static MessageBodyDocument ToDocument(BodyWriteItem entry) => new()
        {
            Id = entry.Id,
            ContentType = entry.ContentType,
            BodySize = entry.BodySize,
            TextBody = entry.TextBody,
            BinaryBody = entry.TextBody == null ? entry.Body : null,
            ExpiresAt = entry.ExpiresAt
        };

        static string TryGetUtf8String(ReadOnlyMemory<byte> body)
        {
            try
            {
                return StrictUtf8Encoding.GetString(body.Span);
            }
            catch
            {
                return null;
            }
        }

        static readonly System.Text.Encoding StrictUtf8Encoding = new System.Text.UTF8Encoding(encoderShouldEmitUTF8Identifier: false, throwOnInvalidBytes: true);
    }
}
