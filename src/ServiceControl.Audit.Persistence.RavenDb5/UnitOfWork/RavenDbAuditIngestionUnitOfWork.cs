namespace ServiceControl.Audit.Persistence.RavenDb.UnitOfWork
{
    using System.Threading.Tasks;
    using Auditing;
    using Monitoring;
    using Persistence.UnitOfWork;
    using Raven.Client.Documents.BulkInsert;
    using ServiceControl.SagaAudit;

    class RavenDbAuditIngestionUnitOfWork : IAuditIngestionUnitOfWork
    {
        BulkInsertOperation bulkInsert;

        public RavenDbAuditIngestionUnitOfWork(BulkInsertOperation bulkInsert)
            => this.bulkInsert = bulkInsert;

        public Task RecordProcessedMessage(ProcessedMessage processedMessage, byte[] body)
            => bulkInsert.StoreAsync(processedMessage);

        public Task RecordSagaSnapshot(SagaSnapshot sagaSnapshot)
            => bulkInsert.StoreAsync(sagaSnapshot);

        public Task RecordKnownEndpoint(KnownEndpoint knownEndpoint)
            => bulkInsert.StoreAsync(knownEndpoint);

        public async ValueTask DisposeAsync()
            => await bulkInsert.DisposeAsync().ConfigureAwait(false);
    }
}