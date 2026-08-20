namespace ServiceControl.Persistence.Tests.AuditCapable
{
    using System;
    using System.Threading;
    using System.Threading.Tasks;
    using ServiceControl.Persistence.Infrastructure;
    using ServiceControl.SagaAudit;

    class InMemorySagaHistoryDataStore(InMemoryAuditStore auditStore) : ISagaHistoryDataStore
    {
        public Task<QueryResult<SagaHistory>> QuerySagaHistoryById(Guid sagaId, CancellationToken cancellationToken = default)
        {
            var history = auditStore.HistoryFor(sagaId);

            return Task.FromResult(history is null
                ? QueryResult<SagaHistory>.Empty()
                : new QueryResult<SagaHistory>(history, new QueryStatsInfo(string.Empty, history.Changes.Count, false)));
        }
    }
}
