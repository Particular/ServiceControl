namespace ServiceControl.CompositeViews.MessageCounting
{
    using System;
    using System.Linq;
    using System.Net.Http;
    using System.Threading.Tasks;
    using Raven.Client;
    using ServiceBus.Management.Infrastructure.Settings;
    using ServiceControl.CompositeViews.Messages;

    class GetAuditCountsApi : ScatterGatherApi<NoInput, DailyAuditCountResult>
    {
        public GetAuditCountsApi(IDocumentStore documentStore, Settings settings, Func<HttpClient> httpClientFactory)
            : base(documentStore, settings, httpClientFactory)
        {
        }

        protected override Task<QueryResult<DailyAuditCountResult>> LocalQuery(HttpRequestMessage request, NoInput input)
        {
            // Will never be implemented on the primary instance
            var result = new QueryResult<DailyAuditCountResult>(null, new QueryStatsInfo(string.Empty, 0));

            return Task.FromResult(result);
        }

        protected override DailyAuditCountResult ProcessResults(HttpRequestMessage request, QueryResult<DailyAuditCountResult>[] results)
        {
            // The "LocalQuery" result will always be null
            var nonNullResults = results.Select(r => r.Results).Where(r => r != null).ToArray();

            var minimumAuditRetention = nonNullResults.Select(r => r.AuditRetention).Min();

            var combined = nonNullResults
                .SelectMany(set => set.Days)
                .GroupBy(e => e.UtcDate)
                .Select(dayGroup => new DailyAuditCount
                {
                    UtcDate = dayGroup.Key,
                    Data = dayGroup.SelectMany(day => day.Data)
                        .GroupBy(ep => ep.Name)
                        .Select(g => new EndpointAuditCount
                        {
                            Name = g.Key,
                            Count = g.Sum(x => x.Count)
                        })
                        .ToArray()
                })
                .OrderBy(day => day.UtcDate)
                .ToArray();

            return new DailyAuditCountResult
            {
                AuditRetention = minimumAuditRetention,
                Days = combined
            };
        }
    }
}
