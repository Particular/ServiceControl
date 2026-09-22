namespace ServiceControl.Persistence
{
    using System;
    using System.Collections.Generic;
    using System.Threading;
    using System.Threading.Tasks;
    using ServiceControl.Api.Contracts;
    using ServiceControl.Persistence.Infrastructure;
    using ServiceControl.SagaAudit;

    // A primary whose persister holds no audit data still serves the audit routes, answering from its
    // remotes alone. These stand in for the local source so the scatter gather is uniform and the APIs
    // do not need to know which persister they are running on.
    /// <summary>
    /// Marks a stand-in whose empty answer says nothing about the data, so the scatter gather does not
    /// count this instance as having answered.
    /// </summary>
    interface IEmptyAuditDataStore;

    class EmptyAuditCountsDataStore : IAuditCountsDataStore, IEmptyAuditDataStore
    {
        public Task<QueryResult<IList<AuditCount>>> QueryAuditCounts(string endpointName, CancellationToken cancellationToken = default) =>
            Task.FromResult(new QueryResult<IList<AuditCount>>(Empty, QueryStatsInfo.Zero));

        static readonly IList<AuditCount> Empty = new List<AuditCount>(0).AsReadOnly();
    }

    class EmptySagaHistoryDataStore : ISagaHistoryDataStore, IEmptyAuditDataStore
    {
        public Task<QueryResult<SagaHistory>> QuerySagaHistoryById(Guid sagaId, PagingInfo pagingInfo, CancellationToken cancellationToken = default) =>
            Task.FromResult(QueryResult<SagaHistory>.Empty());
    }
}
