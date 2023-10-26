namespace ServiceControl.Audit.Persistence.RavenDb.UnitOfWork
{
    using System;
    using System.Threading.Tasks;
    using Auditing;
    using Auditing.BodyStorage;
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
                processedMessage.MessageMetadata["ContentLength"] = body.Length;
                processedMessage.MessageMetadata["BodyUrl"] = $"/messages/{processedMessage.Id}/body";
            }
            else
            {
                processedMessage.MessageMetadata["ContentLength"] = 0;
            }

            await bulkInsert.StoreAsync(processedMessage, GetExpirationMetadata());

            if (body != null)
            {
                using (var stream = ServiceControl.Infrastructure.Memory.Manager.GetStream(Guid.NewGuid(), processedMessage.Id, body, 0, body.Length))
                {
                    if (!processedMessage.Headers.TryGetValue(Headers.ContentType, out var contentType))
                    {
                        contentType = "text/xml";
                    }

                    await bodyStorage.Store(processedMessage.Id, contentType, body.Length, stream);
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
            => bulkInsert.StoreAsync(sagaSnapshot, GetExpirationMetadata());

        public Task RecordKnownEndpoint(KnownEndpoint knownEndpoint)
            => bulkInsert.StoreAsync(knownEndpoint, GetExpirationMetadata());

        public async ValueTask DisposeAsync()
            => await bulkInsert.DisposeAsync();
    }
}