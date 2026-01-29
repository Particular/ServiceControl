namespace ServiceControl.Audit.Persistence.MongoDB.UnitOfWork
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Threading;
    using System.Threading.Tasks;
    using Auditing;
    using Collections;
    using Documents;
    using global::MongoDB.Bson;
    using global::MongoDB.Driver;
    using Monitoring;
    using Persistence.UnitOfWork;
    using ServiceControl.SagaAudit;

    class MongoAuditIngestionUnitOfWork(
        IMongoClient client,
        IMongoDatabase database,
        bool supportsMultiCollectionBulkWrite,
        TimeSpan auditRetentionPeriod)
        : IAuditIngestionUnitOfWork
    {
        readonly List<ProcessedMessageDocument> processedMessages = [];
        readonly List<KnownEndpointDocument> knownEndpoints = [];
        readonly List<SagaSnapshotDocument> sagaSnapshots = [];

        public Task RecordProcessedMessage(ProcessedMessage processedMessage, ReadOnlyMemory<byte> body, CancellationToken cancellationToken)
        {
            processedMessage.MessageMetadata["ContentLength"] = body.Length;

            if (!body.IsEmpty)
            {
                processedMessage.Body = Convert.ToBase64String(body.Span);
                processedMessage.MessageMetadata["BodyUrl"] = $"/messages/{processedMessage.Id}/body";
            }

            processedMessages.Add(new ProcessedMessageDocument
            {
                Id = processedMessage.Id,
                UniqueMessageId = processedMessage.UniqueMessageId,
                MessageMetadata = ConvertToBsonDocument(processedMessage.MessageMetadata),
                Headers = processedMessage.Headers,
                Body = processedMessage.Body,
                ProcessedAt = processedMessage.ProcessedAt,
                ExpiresAt = DateTime.UtcNow.Add(auditRetentionPeriod)
            });

            return Task.CompletedTask;
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
            var tasks = new List<Task>(3);

            if (processedMessages.Count > 0)
            {
                tasks.Add(BulkUpsertAsync(
                    database.GetCollection<ProcessedMessageDocument>(CollectionNames.ProcessedMessages),
                    processedMessages,
                    doc => doc.Id));
            }

            if (knownEndpoints.Count > 0)
            {
                tasks.Add(BulkUpsertAsync(
                    database.GetCollection<KnownEndpointDocument>(CollectionNames.KnownEndpoints),
                    knownEndpoints,
                    doc => doc.Id));
            }

            if (sagaSnapshots.Count > 0)
            {
                tasks.Add(BulkUpsertAsync(
                    database.GetCollection<SagaSnapshotDocument>(CollectionNames.SagaSnapshots),
                    sagaSnapshots,
                    doc => doc.Id));
            }

            await Task.WhenAll(tasks).ConfigureAwait(false);
        }

        // This is a slight misuse of the IAsyncDisposable pattern, however it works fine because the AuditPersister always calls DisposeAsync() in the finally block
        // The method is doing business logic (flushing batched writes to MongoDB) rather than releasing resources.
        // TODO: A cleaner approach would be to have an explicit CommitAsync method on the IAuditIngestionUnitOfWork interface. 
        public async ValueTask DisposeAsync() => await CommitAsync().ConfigureAwait(false);

        static async Task BulkUpsertAsync<T>(IMongoCollection<T> collection, List<T> documents, Func<T, string> idSelector)
        {
            var writes = documents.Select(doc =>
                new ReplaceOneModel<T>(
                    Builders<T>.Filter.Eq("_id", idSelector(doc)),
                    doc)
                { IsUpsert = true });

            _ = await collection.BulkWriteAsync(writes, new BulkWriteOptions { IsOrdered = false }).ConfigureAwait(false);
        }

        static BsonDocument ConvertToBsonDocument(Dictionary<string, object> dictionary)
        {
            var doc = new BsonDocument();
            foreach (var kvp in dictionary)
            {
                doc[kvp.Key] = BsonValue.Create(kvp.Value);
            }
            return doc;
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
