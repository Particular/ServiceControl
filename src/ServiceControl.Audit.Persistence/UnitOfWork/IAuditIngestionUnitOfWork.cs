namespace ServiceControl.Audit.Persistence.UnitOfWork
{
    using System;
    using System.Threading;
    using System.Threading.Tasks;
    using Auditing;
    using Monitoring;
    using ServiceControl.SagaAudit;

    public interface IAuditIngestionUnitOfWork : IAsyncDisposable
    {
        Task RecordProcessedMessage(ProcessedMessage processedMessage, ReadOnlyMemory<byte> body = default, CancellationToken cancellationToken = default);
        Task RecordSagaSnapshot(SagaSnapshot sagaSnapshot, CancellationToken cancellationToken = default);
        Task RecordKnownEndpoint(KnownEndpoint knownEndpoint, CancellationToken cancellationToken = default);
    }
}