namespace ServiceControl.Audit.Persistence.UnitOfWork
{
    using System;
    using System.Threading;
    using System.Threading.Tasks;
    using Auditing;
    using ServiceControl.SagaAudit;

    public interface IAuditIngestionUnitOfWork : IAsyncDisposable
    {
        Task RecordProcessedMessage(ProcessedMessage processedMessage, ReadOnlyMemory<byte> body = default, CancellationToken cancellationToken = default);
        Task RecordSagaSnapshot(SagaSnapshot sagaSnapshot, CancellationToken cancellationToken = default);

        /// <summary>
        /// Signals that all intended records have been added and the unit of work should commit.
        /// </summary>
        Task Complete(CancellationToken cancellationToken = default);
    }
}