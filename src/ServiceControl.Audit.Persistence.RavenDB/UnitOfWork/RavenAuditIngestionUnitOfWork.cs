namespace ServiceControl.Audit.Persistence.RavenDB.UnitOfWork
{
    using System;
    using System.Collections.Generic;
    using System.Threading;
    using System.Threading.Tasks;
    using Auditing;
    using Auditing.BodyStorage;
    using Monitoring;
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
        IBodyStorage bodyStorage)
        : IAuditIngestionUnitOfWork
    {
        public async Task RecordProcessedMessage(ProcessedMessage processedMessage, ReadOnlyMemory<byte> body)
        {
            processedMessage.MessageMetadata["ContentLength"] = body.Length;
            if (!body.IsEmpty)
            {
                processedMessage.MessageMetadata["BodyUrl"] = $"/messages/{processedMessage.Id}/body";
            }

            await bulkInsert.StoreAsync(processedMessage, GetExpirationMetadata());

            if (!body.IsEmpty)
            {
                await using var stream = new ReadOnlyStream(body);
                var contentType = processedMessage.Headers.GetValueOrDefault(Headers.ContentType, "text/plain");

                await bodyStorage.Store(processedMessage.Id, contentType, body.Length, stream);
            }
        }

        MetadataAsDictionary GetExpirationMetadata() =>
            new()
            {
                [Constants.Documents.Metadata.Expires] = DateTime.UtcNow.Add(auditRetentionPeriod)
            };

        public Task RecordSagaSnapshot(SagaSnapshot sagaSnapshot)
            => bulkInsert.StoreAsync(sagaSnapshot, GetExpirationMetadata());

        public Task RecordKnownEndpoint(KnownEndpoint knownEndpoint)
            => bulkInsert.StoreAsync(knownEndpoint, GetExpirationMetadata());

        public async ValueTask DisposeAsync()
        {
            await bulkInsert.DisposeAsync();
            timedCancellationSource.Dispose();
        }
    }
}