namespace ServiceControl.Audit.Persistence.InMemory
{
    using System;
    using System.Threading;
    using System.Threading.Tasks;
    using Auditing.BodyStorage;
    using ServiceControl.Audit.Auditing;
    using ServiceControl.Audit.Persistence.UnitOfWork;
    using ServiceControl.SagaAudit;

    class InMemoryAuditIngestionUnitOfWork(
        InMemoryAuditDataStore dataStore,
        BodyStorageEnricher bodyStorageEnricher)
        : IAuditIngestionUnitOfWork
    {
        public ValueTask DisposeAsync() => ValueTask.CompletedTask;

        public async Task RecordProcessedMessage(ProcessedMessage processedMessage, ReadOnlyMemory<byte> body, CancellationToken cancellationToken)
        {
            if (!body.IsEmpty)
            {
                await bodyStorageEnricher.StoreAuditMessageBody(body, processedMessage, cancellationToken);
            }
            await dataStore.SaveProcessedMessage(processedMessage);
        }

        public Task RecordSagaSnapshot(SagaSnapshot sagaSnapshot, CancellationToken cancellationToken) => dataStore.SaveSagaSnapshot(sagaSnapshot);
    }
}