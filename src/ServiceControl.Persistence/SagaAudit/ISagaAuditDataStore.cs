namespace ServiceControl.Persistence
{
    using System;
    using System.Threading.Tasks;
    using ServiceControl.Persistence.Infrastructure;
    using ServiceControl.SagaAudit;

    public interface ISagaAuditDataStore
    {
        Task StoreSnapshot(SagaSnapshot sagaSnapshot);
        Task<QueryResult<SagaHistory>> GetSagaById(Guid sagaId);
    }
}