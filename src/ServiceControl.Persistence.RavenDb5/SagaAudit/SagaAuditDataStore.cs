namespace ServiceControl.Persistence.RavenDb
{
    using System;
    using System.Threading.Tasks;
    using ServiceControl.Persistence.Infrastructure;
    using ServiceControl.SagaAudit;

    class SagaAuditDataStore : ISagaAuditDataStore
    {
        public Task StoreSnapshot(SagaSnapshot sagaSnapshot) => throw new NotImplementedException();

        public Task<QueryResult<SagaHistory>> GetSagaById(Guid sagaId) => Task.FromResult(QueryResult<SagaHistory>.Empty());
    }
}
