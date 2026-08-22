namespace ServiceControl.Persistence.Tests.AuditCapable
{
    using System.Collections.Generic;
    using System.Linq;
    using System.Threading;
    using System.Threading.Tasks;
    using ServiceControl.Api.Contracts;
    using ServiceControl.Persistence.Infrastructure;

    class InMemoryAuditCountsDataStore(InMemoryAuditStore auditStore) : IAuditCountsDataStore
    {
        public Task<QueryResult<IList<AuditCount>>> QueryAuditCounts(string endpointName, CancellationToken cancellationToken = default)
        {
            IList<AuditCount> counts = [.. auditStore.CountsFor(endpointName).Select(count => new AuditCount { UtcDate = count.UtcDate, Count = count.Count })];

            return Task.FromResult(new QueryResult<IList<AuditCount>>(counts, new QueryStatsInfo(string.Empty, counts.Count, false)));
        }
    }
}
