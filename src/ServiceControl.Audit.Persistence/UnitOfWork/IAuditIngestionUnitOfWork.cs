namespace ServiceControl.Audit.Persistence.UnitOfWork
{
    using System;
    using System.Threading.Tasks;
    using Auditing;
    using Monitoring;
    using ServiceControl.SagaAudit;

    public interface IAuditIngestionUnitOfWork : IAsyncDisposable
    {
        Task RecordProcessedMessage(ProcessedMessage processedMessage, byte[] body = null);
        Task RecordSagaSnapshot(SagaSnapshot sagaSnapshot);
        Task RecordKnownEndpoint(KnownEndpoint knownEndpoint);
    }
}