namespace ServiceControl.Persistence
{
    using System;
    using System.Threading;
    using System.Threading.Tasks;
    using ServiceControl.Persistence.Infrastructure;
    using ServiceControl.SagaAudit;

    public interface ISagaHistoryDataStore
    {
        Task<QueryResult<SagaHistory>> QuerySagaHistoryById(Guid sagaId, CancellationToken cancellationToken = default);
    }
}
