namespace ServiceControl.Audit.Persistence.InMemory
{
    using System.Threading.Tasks;
    using ServiceControl.Audit.Auditing;
    using ServiceControl.Audit.Persistence.Monitoring;
    using ServiceControl.Audit.Persistence.UnitOfWork;
    using ServiceControl.SagaAudit;

    class InMemoryAuditIngestionUnitOfWork : IAuditIngestionUnitOfWork
    {
        public InMemoryAuditIngestionUnitOfWork(InMemoryAuditDataStore dataStore) => this.dataStore = dataStore;

        public ValueTask DisposeAsync()
        {
            return new ValueTask();
        }

        public Task RecordKnownEndpoint(KnownEndpoint knownEndpoint)
        {
            dataStore.knownEndpoints.Add(knownEndpoint);
            return Task.CompletedTask;
        }

        public Task RecordProcessedMessage(ProcessedMessage processedMessage)
        {
            return dataStore.SaveProcessedMessage(processedMessage);
        }

        public Task RecordSagaSnapshot(SagaSnapshot sagaSnapshot)
        {
            return dataStore.SaveSagaSnapshot(sagaSnapshot);
        }

        InMemoryAuditDataStore dataStore;
    }
}