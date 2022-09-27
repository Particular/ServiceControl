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
    using ServiceControl.Audit.Persistence.RavenDb5.Infrastructure;
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
            else
            {
                // TODO: Tests were designed for 3.5 where attachments were a separate thing
                // so tests can generate a processed message with no body. But there is no use
                // case for that outside of tests.
                processedMessage.MessageMetadata["ContentLength"] = 0;
            }

            await bulkInsert.StoreAsync(processedMessage, GetExpirationMetadata()).ConfigureAwait(false);

            if (body != null)
            {
                using (var stream = Memory.Manager.GetStream(Guid.NewGuid(), processedMessage.Id, body, 0, body.Length))
                {
                    if (!processedMessage.Headers.TryGetValue(Headers.ContentType, out var contentType))
                    {
                        // TODO: is there a use case for no content type? and if that's the case is it expected that the default is text/xml?
                        contentType = "text/xml";
                    }

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