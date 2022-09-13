namespace ServiceControl.Audit.Persistence.RavenDb.UnitOfWork
{
    using System;
    using System.Threading.Tasks;
    using Auditing;
    using Infrastructure;
    using Monitoring;
    using NServiceBus;
    using Persistence.UnitOfWork;
    using Raven.Client.Documents.BulkInsert;
    using ServiceControl.SagaAudit;

    class RavenDbAuditIngestionUnitOfWork : IAuditIngestionUnitOfWork
    {
        BulkInsertOperation bulkInsert;

        public RavenDbAuditIngestionUnitOfWork(BulkInsertOperation bulkInsert)
            => this.bulkInsert = bulkInsert;

        public async Task RecordProcessedMessage(ProcessedMessage processedMessage, byte[] body)
        {

            if (body != null)
            {
                // TODO: Pull both of these up to the higher level
                processedMessage.MessageMetadata["ContentLength"] = body.Length;
                processedMessage.MessageMetadata["BodyUrl"] = $"/messages/{processedMessage.Id}/body";
            }

            await bulkInsert.StoreAsync(processedMessage).ConfigureAwait(false);

            if (body != null)
            {
                using (var stream = Memory.Manager.GetStream(Guid.NewGuid(), processedMessage.Id, body, 0, body.Length))
                {
                    processedMessage.Headers.TryGetValue(Headers.ContentType, out var contentType);

                    await bulkInsert.AttachmentsFor(processedMessage.Id)
                        .StoreAsync("body", stream, contentType)
                        .ConfigureAwait(false);
                }
            }
        }

        public Task RecordSagaSnapshot(SagaSnapshot sagaSnapshot)
            => bulkInsert.StoreAsync(sagaSnapshot);

        public Task RecordKnownEndpoint(KnownEndpoint knownEndpoint)
            => bulkInsert.StoreAsync(knownEndpoint);

        public async ValueTask DisposeAsync()
            => await bulkInsert.DisposeAsync().ConfigureAwait(false);
    }
}