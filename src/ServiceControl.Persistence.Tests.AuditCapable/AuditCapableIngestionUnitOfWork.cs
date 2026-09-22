namespace ServiceControl.Persistence.Tests.AuditCapable
{
    using System;
    using System.Collections.Concurrent;
    using System.Threading;
    using System.Threading.Tasks;
    using ServiceControl.MessageAuditing;
    using ServiceControl.Persistence.UnitOfWork;
    using ServiceControl.SagaAudit;

    // Recording is buffered and only visible after Complete, so tests see the same all or nothing
    // batch behaviour a real persister gives them.
    class AuditCapableIngestionUnitOfWork(IIngestionUnitOfWork inner, InMemoryAuditStore auditStore)
        : IIngestionUnitOfWork, IAuditIngestionUnitOfWork
    {
        readonly ConcurrentQueue<(ProcessedMessage Message, byte[] Body)> processedMessages = new();
        readonly ConcurrentQueue<SagaSnapshot> sagaSnapshots = new();

        public IMonitoringIngestionUnitOfWork? Monitoring => inner.Monitoring;

        public IRecoverabilityIngestionUnitOfWork? Recoverability => inner.Recoverability;

        public IAuditIngestionUnitOfWork? Audit => this;

        public Task RecordProcessedMessage(ProcessedMessage processedMessage, ReadOnlyMemory<byte> body = default, CancellationToken cancellationToken = default)
        {
            processedMessages.Enqueue((processedMessage, body.ToArray()));
            return Task.CompletedTask;
        }

        public Task RecordSagaSnapshot(SagaSnapshot sagaSnapshot, CancellationToken cancellationToken = default)
        {
            sagaSnapshots.Enqueue(sagaSnapshot);
            return Task.CompletedTask;
        }

        public async Task Complete(CancellationToken cancellationToken = default)
        {
            await inner.Complete(cancellationToken);

            while (processedMessages.TryDequeue(out var processedMessage))
            {
                auditStore.Record(processedMessage.Message, processedMessage.Body);
            }

            while (sagaSnapshots.TryDequeue(out var sagaSnapshot))
            {
                auditStore.Record(sagaSnapshot);
            }
        }

        public ValueTask DisposeAsync() => inner.DisposeAsync();
    }
}
