namespace ServiceControl.Audit.Persistence.RavenDb.UnitOfWork
{
    using System.Threading.Tasks;
    using Auditing;
    using Monitoring;
    using Persistence.UnitOfWork;
    using Raven.Client.Document;
    using ServiceControl.Audit.Auditing.BodyStorage;
    using ServiceControl.SagaAudit;

    class RavenDbAuditIngestionUnitOfWork : IAuditIngestionUnitOfWork
    {
        BulkInsertOperation bulkInsert;
        BodyStorageEnricher bodyStorageEnricher;

        public RavenDbAuditIngestionUnitOfWork(BulkInsertOperation bulkInsert, BodyStorageEnricher bodyStorageEnricher)
        {
            this.bulkInsert = bulkInsert;
            this.bodyStorageEnricher = bodyStorageEnricher;
        }

        public async Task RecordProcessedMessage(ProcessedMessage processedMessage, byte[] body)
        {
            if (body != null)
            {
                await bodyStorageEnricher.StoreAuditMessageBody(body, processedMessage)
                .ConfigureAwait(false);
            }

            await bulkInsert.StoreAsync(processedMessage)
                .ConfigureAwait(false);
        }

        public Task RecordSagaSnapshot(SagaSnapshot sagaSnapshot)
            => bulkInsert.StoreAsync(sagaSnapshot);

        public Task RecordKnownEndpoint(KnownEndpoint knownEndpoint)
            => bulkInsert.StoreAsync(knownEndpoint);

        public async ValueTask DisposeAsync()
            => await bulkInsert.DisposeAsync().ConfigureAwait(false);
    }
}