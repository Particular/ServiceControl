namespace ServiceControl.Audit.Persistence
{
    using System;
    using System.Threading;
    using System.Threading.Tasks;
    using ServiceControl.Audit.Auditing.MessagesView;
    using ServiceControl.SagaAudit;

    public interface ISagaHistoryDataStore
    {
        Task<QueryResult<SagaHistory>> QuerySagaHistoryById(Guid input, CancellationToken cancellationToken = default);
    }
}