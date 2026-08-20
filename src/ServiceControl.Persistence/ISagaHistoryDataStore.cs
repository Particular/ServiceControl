namespace ServiceControl.Persistence
{
    using System;
    using System.Threading;
    using System.Threading.Tasks;
    using ServiceControl.Persistence.Infrastructure;
    using ServiceControl.SagaAudit;

    public interface ISagaHistoryDataStore
    {
        /// <summary>
        /// One page of a saga's state changes, newest first. A long lived saga accumulates a snapshot
        /// per state change with no natural bound, so the page is what keeps the response finite.
        /// <see cref="QueryStatsInfo.TotalCount"/> reports how many changes the saga has, not how many
        /// this page carries.
        /// </summary>
        Task<QueryResult<SagaHistory>> QuerySagaHistoryById(Guid sagaId, PagingInfo pagingInfo, CancellationToken cancellationToken = default);
    }
}
