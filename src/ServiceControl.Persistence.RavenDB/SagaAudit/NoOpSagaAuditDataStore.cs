namespace ServiceControl.Persistence.RavenDB
{
    using System;
    using System.Threading.Tasks;
    using ServiceControl.Persistence.Infrastructure;
    using ServiceControl.SagaAudit;

    class NoOpSagaAuditDataStore : ISagaAuditDataStore
    {
        public Task<bool> StoreSnapshot(SagaSnapshot sagaSnapshot) => Task.FromResult(false);

        public Task<QueryResult<SagaHistory>> GetSagaById(Guid sagaId) => Task.FromResult(QueryResult<SagaHistory>.Empty());
    }
}
