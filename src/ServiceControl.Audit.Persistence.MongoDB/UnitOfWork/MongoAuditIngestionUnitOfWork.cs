namespace ServiceControl.Audit.Persistence.MongoDB.UnitOfWork
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Text;
    using System.Threading;
    using System.Threading.Tasks;
    using Auditing;
    using BodyStorage;
    using Collections;
    using Documents;
    using global::MongoDB.Bson;
    using global::MongoDB.Driver;
    using Monitoring;
    using NServiceBus;
    using Microsoft.Extensions.Logging;
    using Persistence.UnitOfWork;
    using ServiceControl.SagaAudit;

    class MongoAuditIngestionUnitOfWork(
        IMongoClient client,
        IMongoDatabase database,
        bool supportsMultiCollectionBulkWrite,
        TimeSpan auditRetentionPeriod,
        int maxBodySizeToStore,
        bool storeBodyInline,
        IBodyWriter bodyEntryWriter,
        ILogger<MongoAuditIngestionUnitOfWork> logger)
        : IAuditIngestionUnitOfWork
    {
        readonly List<ProcessedMessageDocument> processedMessages = [];
        readonly List<KnownEndpointDocument> knownEndpoints = [];
        readonly List<SagaSnapshotDocument> sagaSnapshots = [];

        public async Task RecordProcessedMessage(ProcessedMessage processedMessage, ReadOnlyMemory<byte> body, CancellationToken cancellationToken)
        {
            processedMessage.MessageMetadata["ContentLength"] = body.Length;

            var shouldStoreBody = !body.IsEmpty && body.Length <= maxBodySizeToStore && (storeBodyInline || bodyEntryWriter.IsEnabled);

            string inlineContentType = null;
            string inlineTextBody = null;
            byte[] inlineBinaryBody = null;

            if (shouldStoreBody)
            {
                processedMessage.MessageMetadata["BodyUrl"] = $"/messages/{processedMessage.Id}/body";
                var contentType = processedMessage.Headers.GetValueOrDefault(Headers.ContentType, "text/plain").Trim();

                if (storeBodyInline)
                {
                    inlineContentType = contentType;
                    if (MessageBodyDocument.IsTextContentType(contentType))
                    {
                        inlineTextBody = TryGetUtf8String(body);
                        if (inlineTextBody == null)
                        {
                            inlineBinaryBody = body.ToArray();
                        }
                    }
                    else
                    {
                        inlineBinaryBody = body.ToArray();
                    }
                }
                else
                {
                    var bodyExpiresAt = DateTime.UtcNow.Add(auditRetentionPeriod);
                    await bodyEntryWriter.WriteAsync(processedMessage.Id, contentType, body, bodyExpiresAt, cancellationToken).ConfigureAwait(false);
                }
            }

            var expiresAt = DateTime.UtcNow.Add(auditRetentionPeriod);

            processedMessages.Add(new ProcessedMessageDocument
            {
                Id = processedMessage.Id,
                UniqueMessageId = processedMessage.UniqueMessageId,
                MessageMetadata = ConvertToBsonDocument(processedMessage.MessageMetadata),
                Headers = processedMessage.Headers,
                HeaderSearchTokens = processedMessage.Headers?.Values
                    .Where(v => !string.IsNullOrWhiteSpace(v))
                    .ToList(),
                ProcessedAt = processedMessage.ProcessedAt,
                ExpiresAt = expiresAt,
                BodyContentType = inlineContentType,
                BodySize = shouldStoreBody ? body.Length : 0,
                TextBody = inlineTextBody,
                BinaryBody = inlineBinaryBody
            });
        }

        public Task RecordKnownEndpoint(KnownEndpoint knownEndpoint, CancellationToken cancellationToken)
        {
            knownEndpoints.Add(new KnownEndpointDocument
            {
                Id = KnownEndpoint.MakeDocumentId(knownEndpoint.Name, knownEndpoint.HostId),
                Name = knownEndpoint.Name,
                HostId = knownEndpoint.HostId,
                Host = knownEndpoint.Host,
                LastSeen = knownEndpoint.LastSeen,
                ExpiresAt = DateTime.UtcNow.Add(auditRetentionPeriod)
            });

            return Task.CompletedTask;
        }

        public Task RecordSagaSnapshot(SagaSnapshot sagaSnapshot, CancellationToken cancellationToken)
        {
            sagaSnapshots.Add(new SagaSnapshotDocument
            {
                // TODO: Verify this ID assignment logic. Will the sagaSnapshot.Id ever be null, or is it always null resulting in a new ObjectId being generated every time.
                Id = sagaSnapshot.Id ?? ObjectId.GenerateNewId().ToString(),
                SagaId = sagaSnapshot.SagaId,
                SagaType = sagaSnapshot.SagaType,
                StartTime = sagaSnapshot.StartTime,
                FinishTime = sagaSnapshot.FinishTime,
                Status = sagaSnapshot.Status,
                StateAfterChange = sagaSnapshot.StateAfterChange,
                InitiatingMessage = sagaSnapshot.InitiatingMessage != null ? ToDocument(sagaSnapshot.InitiatingMessage) : null,
                OutgoingMessages = sagaSnapshot.OutgoingMessages.Select(ToDocument).ToList(),
                Endpoint = sagaSnapshot.Endpoint,
                ProcessedAt = sagaSnapshot.ProcessedAt,
                ExpiresAt = DateTime.UtcNow.Add(auditRetentionPeriod)
            });

            return Task.CompletedTask;
        }

        public async Task CommitAsync()
        {
            if (processedMessages.Count == 0 && knownEndpoints.Count == 0 && sagaSnapshots.Count == 0)
            {
                return;
            }

            if (supportsMultiCollectionBulkWrite)
            {
                await CommitWithMultiCollectionBulkWriteAsync().ConfigureAwait(false);
            }
            else
            {
                await CommitWithParallelBulkWritesAsync().ConfigureAwait(false);
            }
        }

        async Task CommitWithMultiCollectionBulkWriteAsync()
        {
            var models = new List<BulkWriteModel>();
            var databaseName = database.DatabaseNamespace.DatabaseName;

            foreach (var doc in processedMessages)
            {
                var ns = new CollectionNamespace(databaseName, CollectionNames.ProcessedMessages);
                var filter = Builders<ProcessedMessageDocument>.Filter.Eq(d => d.Id, doc.Id);
                models.Add(new BulkWriteReplaceOneModel<ProcessedMessageDocument>(ns, filter, doc) { IsUpsert = true });
            }

            foreach (var doc in knownEndpoints)
            {
                var ns = new CollectionNamespace(databaseName, CollectionNames.KnownEndpoints);
                var filter = Builders<KnownEndpointDocument>.Filter.Eq(d => d.Id, doc.Id);
                models.Add(new BulkWriteReplaceOneModel<KnownEndpointDocument>(ns, filter, doc) { IsUpsert = true });
            }

            foreach (var doc in sagaSnapshots)
            {
                var ns = new CollectionNamespace(databaseName, CollectionNames.SagaSnapshots);
                var filter = Builders<SagaSnapshotDocument>.Filter.Eq(d => d.Id, doc.Id);
                models.Add(new BulkWriteReplaceOneModel<SagaSnapshotDocument>(ns, filter, doc) { IsUpsert = true });
            }

            _ = await client.BulkWriteAsync(models, new ClientBulkWriteOptions { IsOrdered = false }).ConfigureAwait(false);
        }

        async Task CommitWithParallelBulkWritesAsync()
        {
            // Sequential writes to avoid cross-collection deadlocks
            if (processedMessages.Count > 0)
            {
                await BulkUpsertAsync(
                    database.GetCollection<ProcessedMessageDocument>(CollectionNames.ProcessedMessages),
                    processedMessages,
                    doc => doc.Id).ConfigureAwait(false);
            }

            if (knownEndpoints.Count > 0)
            {
                await BulkUpsertAsync(
                    database.GetCollection<KnownEndpointDocument>(CollectionNames.KnownEndpoints),
                    knownEndpoints,
                    doc => doc.Id).ConfigureAwait(false);
            }

            if (sagaSnapshots.Count > 0)
            {
                await BulkUpsertAsync(
                    database.GetCollection<SagaSnapshotDocument>(CollectionNames.SagaSnapshots),
                    sagaSnapshots,
                    doc => doc.Id).ConfigureAwait(false);
            }
        }

        // This is a slight misuse of the IAsyncDisposable pattern, however it works fine because the AuditPersister always calls DisposeAsync() in the finally block
        // The method is doing business logic (flushing batched writes to MongoDB) rather than releasing resources.
        // TODO: A cleaner approach would be to have an explicit CommitAsync method on the IAuditIngestionUnitOfWork interface. 
        public async ValueTask DisposeAsync() => await CommitAsync().ConfigureAwait(false);

        const int MaxRetries = 3;

        async Task BulkUpsertAsync<T>(IMongoCollection<T> collection, List<T> documents, Func<T, string> idSelector)
        {
            // Optimistic insert: most audit messages are new, so insert is the fast path
            // (no _id lookup required). Fall back to upsert only for duplicates.
            var inserts = documents.Select(doc => new InsertOneModel<T>(doc)).ToList<WriteModel<T>>();

            for (var attempt = 1; attempt <= MaxRetries; attempt++)
            {
                try
                {
                    _ = await collection.BulkWriteAsync(inserts, new BulkWriteOptions { IsOrdered = false }).ConfigureAwait(false);
                    return;
                }
                catch (MongoBulkWriteException<T> ex) when (ex.WriteErrors.Count > 0 && ex.WriteErrors.All(e => e.Category == ServerErrorCategory.DuplicateKey))
                {
                    // Collect the documents that already exist and need to be overwritten
                    var duplicateIndexes = new HashSet<int>(ex.WriteErrors.Select(e => e.Index));
                    var upserts = duplicateIndexes
                        .Select(i => new ReplaceOneModel<T>(
                            Builders<T>.Filter.Eq("_id", idSelector(documents[i])),
                            documents[i])
                        { IsUpsert = true })
                        .ToList<WriteModel<T>>();

                    logger.LogDebug("Upserting {Count} duplicate documents on {Collection}",
                        upserts.Count, collection.CollectionNamespace.CollectionName);

                    _ = await collection.BulkWriteAsync(upserts, new BulkWriteOptions { IsOrdered = false }).ConfigureAwait(false);
                    return;
                }
                catch (MongoCommandException ex) when (attempt < MaxRetries)
                {
                    var delay = TimeSpan.FromSeconds(Math.Pow(2, attempt - 1));
                    logger.LogWarning(ex, "Deadlock detected on {Collection} ({Count} documents, attempt {Attempt}/{MaxRetries}), retrying in {Delay}s",
                        collection.CollectionNamespace.CollectionName, documents.Count, attempt, MaxRetries, delay.TotalSeconds);
                    await Task.Delay(delay).ConfigureAwait(false);
                }
            }
        }

        static BsonDocument ConvertToBsonDocument(Dictionary<string, object> dictionary)
        {
            var doc = new BsonDocument();
            foreach (var kvp in dictionary)
            {
                doc[kvp.Key] = ConvertToBsonValue(kvp.Value);
            }
            return doc;
        }

        // mostly here to handle special types not natively supported by BsonTypeMapper
        static BsonValue ConvertToBsonValue(object value)
        {
            if (value == null)
            {
                return BsonNull.Value;
            }

            // Handle types that need special conversion
            if (value is TimeSpan ts)
            {
                return ts.ToString();
            }

            if (value is DateTimeOffset dto)
            {
                return dto.UtcDateTime;
            }

            // Guids - convert to string to avoid BinaryData serialization issues
            // This is also consistent with how RavenDB stores Guids in metadata
            if (value is Guid guid)
            {
                return guid.ToString();
            }

            // Enums - convert to string for readability
            if (value.GetType().IsEnum)
            {
                return value.ToString();
            }

            // Nested dictionaries need recursive conversion to handle special types like TimeSpan
            if (value is IDictionary<string, object> dict)
            {
                return ConvertToBsonDocument(dict as Dictionary<string, object> ?? new Dictionary<string, object>(dict));
            }

            // Lists/Arrays - recursively convert items to handle special types
            if (value is System.Collections.IEnumerable enumerable and not string and not byte[])
            {
                var array = new BsonArray();
                foreach (var item in enumerable)
                {
                    array.Add(ConvertToBsonValue(item));
                }
                return array;
            }

            // Try BsonTypeMapper for natively supported types (primitives, string, DateTime, Guid, byte[], etc.)
            if (BsonTypeMapper.TryMapToBsonValue(value, out var bsonValue))
            {
                return bsonValue;
            }

            // Complex objects (like EndpointDetails) - serialize to BsonDocument
            return value.ToBsonDocument(value.GetType());
        }

        static readonly Encoding StrictUtf8Encoding = new UTF8Encoding(encoderShouldEmitUTF8Identifier: false, throwOnInvalidBytes: true);

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

        static InitiatingMessageDocument ToDocument(InitiatingMessage msg) => new()
        {
            MessageId = msg.MessageId,
            MessageType = msg.MessageType,
            IsSagaTimeoutMessage = msg.IsSagaTimeoutMessage,
            OriginatingMachine = msg.OriginatingMachine,
            OriginatingEndpoint = msg.OriginatingEndpoint,
            TimeSent = msg.TimeSent,
            Intent = msg.Intent
        };

        static ResultingMessageDocument ToDocument(ResultingMessage msg) => new()
        {
            MessageId = msg.MessageId,
            MessageType = msg.MessageType,
            Destination = msg.Destination,
            TimeSent = msg.TimeSent,
            Intent = msg.Intent,
            DeliveryDelay = msg.DeliveryDelay?.ToString(),
            DeliverAt = msg.DeliverAt
        };
    }
}
