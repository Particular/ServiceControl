namespace ServiceControl.Audit.Persistence.InMemory
{
    using System.Threading.Tasks;
    using ServiceControl.Audit.Auditing;
    using ServiceControl.Audit.Monitoring;
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
            dataStore.processedMessages.Add(processedMessage);
            return Task.CompletedTask;
        }
        public Task RecordSagaSnapshot(SagaSnapshot sagaSnapshot)
        {
            //TODO
            //dataStore.sagaHistories.Add(sagaSnapshot);
            return Task.CompletedTask;
        }

        InMemoryAuditDataStore dataStore;

    }
}