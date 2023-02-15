namespace ServiceControl.CompositeViews.MessageCounting
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Net.Http;
    using System.Threading.Tasks;
    using Raven.Client;
    using ServiceBus.Management.Infrastructure.Settings;
    using ServiceControl.CompositeViews.Messages;

    class GetAuditCountsForEndpointApi : ScatterGatherApi<string, IList<AuditCount>>
    {
        static readonly IList<AuditCount> empty = new List<AuditCount>().AsReadOnly();

        public GetAuditCountsForEndpointApi(IDocumentStore documentStore, Settings settings, Func<HttpClient> httpClientFactory)
            : base(documentStore, settings, httpClientFactory)
        {
        }

        protected override Task<QueryResult<IList<AuditCount>>> LocalQuery(HttpRequestMessage request, string input)
        {
            // Will never be implemented on the primary instance
            return Task.FromResult(new QueryResult<IList<AuditCount>>(empty, QueryStatsInfo.Zero));
        }

        protected override IList<AuditCount> ProcessResults(HttpRequestMessage request, QueryResult<IList<AuditCount>>[] results)
        {
            return results.SelectMany(r => r.Results)
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
}
