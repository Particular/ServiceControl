namespace ServiceControl.Audit.Persistence.Postgresql.UnitOfWork
{
    using System;
    using System.Threading;
    using System.Threading.Tasks;
    using ServiceControl.Audit.Auditing;
    using ServiceControl.Audit.Persistence.Monitoring;
    using ServiceControl.Audit.Persistence.UnitOfWork;
    using ServiceControl.SagaAudit;

    public class PostgresqlAuditIngestionUnitOfWork : IAuditIngestionUnitOfWork
    {
        public ValueTask DisposeAsync()
        {
            // TODO: Dispose resources if needed
            return ValueTask.CompletedTask;
        }

        public Task CompleteAsync(CancellationToken cancellationToken = default)
        {
            // TODO: Commit transaction or batch
            throw new NotImplementedException();
        }

        public Task RecordProcessedMessage(ProcessedMessage processedMessage, ReadOnlyMemory<byte> body = default, CancellationToken cancellationToken = default) => throw new NotImplementedException();
        public Task RecordSagaSnapshot(SagaSnapshot sagaSnapshot, CancellationToken cancellationToken = default) => throw new NotImplementedException();
        public Task RecordKnownEndpoint(KnownEndpoint knownEndpoint, CancellationToken cancellationToken = default) => throw new NotImplementedException();
    }
}
