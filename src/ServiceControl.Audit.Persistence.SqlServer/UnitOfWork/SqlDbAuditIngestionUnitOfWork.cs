namespace ServiceControl.Audit.Persistence.SqlServer.UnitOfWork
{
    using System.Threading.Tasks;
    using Auditing;
    using Monitoring;
    using Persistence.UnitOfWork;
    using ServiceControl.SagaAudit;

    class SqlDbAuditIngestionUnitOfWork : IAuditIngestionUnitOfWork
    {
        public ValueTask DisposeAsync() => throw new System.NotImplementedException();

        public Task RecordProcessedMessage(ProcessedMessage processedMessage, byte[] body) => throw new System.NotImplementedException();

        public Task RecordSagaSnapshot(SagaSnapshot sagaSnapshot) => throw new System.NotImplementedException();

        public Task RecordKnownEndpoint(KnownEndpoint knownEndpoint) => throw new System.NotImplementedException();
    }
}