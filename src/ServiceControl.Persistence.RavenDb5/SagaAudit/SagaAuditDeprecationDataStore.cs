namespace ServiceControl.Persistence.RavenDb
{
    using System;
    using System.Threading.Tasks;
    using ServiceControl.Persistence.Infrastructure;
    using ServiceControl.SagaAudit;

    class SagaAuditDeprecationDataStore : ISagaAuditDataStore
    {
        public SagaAuditDeprecationDataStore(SagaAuditDestinationCustomCheck.State customCheckState)
        {
            this.customCheckState = customCheckState;
        }

        public Task<bool> StoreSnapshot(SagaSnapshot sagaSnapshot)
        {
            customCheckState.Fail(sagaSnapshot.Endpoint);
            return Task.FromResult(false);
        }

        public Task<QueryResult<SagaHistory>> GetSagaById(Guid sagaId) => Task.FromResult(QueryResult<SagaHistory>.Empty());

        readonly SagaAuditDestinationCustomCheck.State customCheckState;
    }
}
