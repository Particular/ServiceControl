namespace ServiceControl.Audit.Persistence.InMemory
{
    using System;
    using System.Threading.Tasks;
    using Auditing.BodyStorage;
    using ServiceControl.Audit.Auditing;
    using ServiceControl.Audit.Persistence.Monitoring;
    using ServiceControl.Audit.Persistence.UnitOfWork;
    using ServiceControl.SagaAudit;

    class InMemoryAuditIngestionUnitOfWork(
        InMemoryAuditDataStore dataStore,
        BodyStorageEnricher bodyStorageEnricher)
        : IAuditIngestionUnitOfWork
    {
        public ValueTask DisposeAsync() => ValueTask.CompletedTask;

        public Task RecordKnownEndpoint(KnownEndpoint knownEndpoint)
        {
            dataStore.knownEndpoints.Add(knownEndpoint);
            return Task.CompletedTask;
        }

        public async Task RecordProcessedMessage(ProcessedMessage processedMessage, ReadOnlyMemory<byte> body)
        {
            if (body.Length > 0)
            {
                await bodyStorageEnricher.StoreAuditMessageBody(body, processedMessage);
            }
            await dataStore.SaveProcessedMessage(processedMessage);
        }

        public Task RecordSagaSnapshot(SagaSnapshot sagaSnapshot) => dataStore.SaveSagaSnapshot(sagaSnapshot);
    }
}