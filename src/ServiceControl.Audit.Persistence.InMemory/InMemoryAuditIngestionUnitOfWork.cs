namespace ServiceControl.Audit.Persistence.InMemory
{
    using System.Threading.Tasks;
    using Auditing.BodyStorage;
    using ServiceControl.Audit.Auditing;
    using ServiceControl.Audit.Monitoring;
    using ServiceControl.Audit.Persistence.UnitOfWork;
    using ServiceControl.SagaAudit;

    class InMemoryAuditIngestionUnitOfWork : IAuditIngestionUnitOfWork
    {
        public InMemoryAuditIngestionUnitOfWork(InMemoryAuditDataStore dataStore,
            BodyStorageEnricher bodyStorageEnricher)
        {
            this.dataStore = dataStore;
            this.bodyStorageEnricher = bodyStorageEnricher;
        }

        public ValueTask DisposeAsync()
        {
            return new ValueTask();
        }

        public Task RecordKnownEndpoint(KnownEndpoint knownEndpoint)
        {
            dataStore.knownEndpoints.Add(knownEndpoint);
            return Task.CompletedTask;
        }

        public async Task RecordProcessedMessage(ProcessedMessage processedMessage, byte[] body)
        {
            if (body != null)
            {
                await bodyStorageEnricher.StoreAuditMessageBody(body, processedMessage).ConfigureAwait(false);
            }
            await dataStore.SaveProcessedMessage(processedMessage).ConfigureAwait(false);
        }

        public Task RecordSagaSnapshot(SagaSnapshot sagaSnapshot)
        {
            return dataStore.SaveSagaSnapshot(sagaSnapshot);
        }

        InMemoryAuditDataStore dataStore;
        BodyStorageEnricher bodyStorageEnricher;
    }
}