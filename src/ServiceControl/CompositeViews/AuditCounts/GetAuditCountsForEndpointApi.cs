namespace ServiceControl.CompositeViews.MessageCounting
{
    using System.Collections.Generic;
    using System.Linq;
    using System.Net.Http;
    using System.Threading.Tasks;
    using Microsoft.AspNetCore.Http;
    using ServiceBus.Management.Infrastructure.Settings;
    using ServiceControl.CompositeViews.Messages;
    using ServiceControl.Persistence;
    using ServiceControl.Persistence.Infrastructure;

    public class GetAuditCountsForEndpointApi : ScatterGatherApi<IErrorMessageDataStore, ScatterGatherContext, IList<AuditCount>>
    {
        static readonly IList<AuditCount> Empty = new List<AuditCount>(0).AsReadOnly();

        public GetAuditCountsForEndpointApi(IErrorMessageDataStore dataStore, Settings settings, IHttpClientFactory httpClientFactory, IHttpContextAccessor httpContextAccessor)
            : base(dataStore, settings, httpClientFactory, httpContextAccessor)
        {
        }

        protected override Task<QueryResult<IList<AuditCount>>> LocalQuery(ScatterGatherContext input) =>
            // Will never be implemented on the primary instance
            Task.FromResult(new QueryResult<IList<AuditCount>>(Empty, QueryStatsInfo.Zero));

        protected override IList<AuditCount> ProcessResults(ScatterGatherContext input, QueryResult<IList<AuditCount>>[] results) =>
            results.SelectMany(r => r.Results)
                .GroupBy(r => r.UtcDate)
                .Select(g => new AuditCount
                {
                    UtcDate = g.Key,
                    Count = g.Sum(r => r.Count)
                })
                .OrderBy(r => r.UtcDate)
                .ToList();
    }
}
