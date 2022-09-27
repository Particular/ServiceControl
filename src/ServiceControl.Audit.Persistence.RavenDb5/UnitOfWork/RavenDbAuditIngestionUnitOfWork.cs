namespace ServiceControl.Audit.Persistence.RavenDb.UnitOfWork
{
    using System;
    using System.Threading.Tasks;
    using Auditing;
    using Auditing.BodyStorage;
    using Infrastructure;
    using Monitoring;
    using NServiceBus;
    using Persistence.UnitOfWork;
    using Raven.Client;
    using Raven.Client.Documents.BulkInsert;
    using Raven.Client.Json;
    using ServiceControl.SagaAudit;

    class RavenDbAuditIngestionUnitOfWork : IAuditIngestionUnitOfWork
    {
        BulkInsertOperation bulkInsert;
        TimeSpan auditRetentionPeriod;
        IBodyStorage bodyStorage;

        public RavenDbAuditIngestionUnitOfWork(BulkInsertOperation bulkInsert, TimeSpan auditRetentionPeriod, IBodyStorage bodyStorage)
        {
            this.bulkInsert = bulkInsert;
            this.auditRetentionPeriod = auditRetentionPeriod;
            this.bodyStorage = bodyStorage;
        }

        public async Task RecordProcessedMessage(ProcessedMessage processedMessage, byte[] body)
        {

            if (body != null)
            {
                // TODO: Pull both of these up to the higher level
                processedMessage.MessageMetadata["ContentLength"] = body.Length;
                processedMessage.MessageMetadata["BodyUrl"] = $"/messages/{processedMessage.Id}/body";
            }

            await bulkInsert.StoreAsync(processedMessage, GetExpirationMetadata()).ConfigureAwait(false);

            if (body != null)
            {
                using (var stream = Memory.Manager.GetStream(Guid.NewGuid(), processedMessage.Id, body, 0, body.Length))
                {
                    processedMessage.Headers.TryGetValue(Headers.ContentType, out var contentType);

                    await bodyStorage.Store(processedMessage.Id, contentType, body.Length, stream).ConfigureAwait(false);
                }
            }
        }

        MetadataAsDictionary GetExpirationMetadata()
        {
            return new MetadataAsDictionary
            {
                [Constants.Documents.Metadata.Expires] = DateTime.UtcNow.Add(auditRetentionPeriod)
            };
        }

        public Task RecordSagaSnapshot(SagaSnapshot sagaSnapshot)
            => bulkInsert.StoreAsync(sagaSnapshot);

        public Task RecordKnownEndpoint(KnownEndpoint knownEndpoint)
            => bulkInsert.StoreAsync(knownEndpoint, GetExpirationMetadata());

        public async ValueTask DisposeAsync()
            => await bulkInsert.DisposeAsync().ConfigureAwait(false);
    }
}