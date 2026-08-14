namespace ServiceControl.Audit.Persistence.RavenDB.UnitOfWork
{
    using System;
    using System.Collections.Generic;
    using System.Threading;
    using System.Threading.Tasks;
    using AuditRetentionBuckets;
    using Auditing;
    using Auditing.BodyStorage;
    using NServiceBus;
    using Persistence.UnitOfWork;
    using Raven.Client;
    using Raven.Client.Documents.BulkInsert;
    using Raven.Client.Json;
    using ServiceControl.Infrastructure;
    using ServiceControl.SagaAudit;

    class RavenAuditIngestionUnitOfWork(
        BulkInsertOperation bulkInsert,
        CancellationTokenSource timedCancellationSource,
        TimeSpan auditRetentionPeriod,
        IBodyStorage bodyStorage,
        AuditRetentionBucket currentBucket)
        : IAuditIngestionUnitOfWork
    {
        public async Task RecordProcessedMessage(ProcessedMessage processedMessage, ReadOnlyMemory<byte> body, CancellationToken cancellationToken = default)
        {
            processedMessage.MessageMetadata["ContentLength"] = body.Length;
            if (!body.IsEmpty)
            {
                processedMessage.MessageMetadata["BodyUrl"] = $"/messages/{processedMessage.Id}/body";
            }

            await bulkInsert.StoreAsync(processedMessage, GetMetadata());

            if (!body.IsEmpty)
            {
                await using var stream = new ReadOnlyStream(body);
                var contentType = processedMessage.Headers.GetValueOrDefault(Headers.ContentType, "text/plain");

                await bodyStorage.Store(processedMessage.Id, contentType, body.Length, stream, cancellationToken);
            }
        }

        public Task RecordSagaSnapshot(SagaSnapshot sagaSnapshot, CancellationToken cancellationToken = default)
        {
            if (currentBucket == null)
            {
                return bulkInsert.StoreAsync(sagaSnapshot, GetExpirationMetadata());
            }

            return bulkInsert.StoreAsync(sagaSnapshot, new MetadataAsDictionary
            {
                [Constants.Documents.Metadata.Collection] = currentBucket.SagaSnapshotCollection
            });
        }

        MetadataAsDictionary GetMetadata()
        {
            if (currentBucket == null)
            {
                return GetExpirationMetadata();
            }

            return new MetadataAsDictionary
            {
                [Constants.Documents.Metadata.Collection] = currentBucket.ProcessedMessageCollection
            };
        }

        MetadataAsDictionary GetExpirationMetadata() =>
            new()
            {
                [Constants.Documents.Metadata.Expires] = DateTime.UtcNow.Add(auditRetentionPeriod)
            };

        public async ValueTask DisposeAsync()
        {
            await bulkInsert.DisposeAsync();
            timedCancellationSource.Dispose();
        }
    }
}
