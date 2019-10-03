namespace ServiceControl.SagaAudit
{
    using System;
    using System.Linq;
    using System.Net.Http;
    using System.Threading.Tasks;
    using CompositeViews.Messages;
    using Raven.Client;
    using ServiceBus.Management.Infrastructure.Settings;

    class GetSagaByIdApi : ScatterGatherApi<Guid, SagaHistory>
    {
        public GetSagaByIdApi(IDocumentStore documentStore, Settings settings, Func<HttpClient> httpClientFactory) : base(documentStore, settings, httpClientFactory)
        {
        }

        protected override async Task<QueryResult<SagaHistory>> LocalQuery(HttpRequestMessage request, Guid input)
        {
            using (var session = Store.OpenAsyncSession())
            {
                var sagaHistory = await
                    session.Query<SagaHistory, SagaDetailsIndex>()
                        .Statistics(out var stats)
                        .SingleOrDefaultAsync(x => x.SagaId == input)
                        .ConfigureAwait(false);

                if (sagaHistory == null)
                {
                    return QueryResult<SagaHistory>.Empty();
                }

                return new QueryResult<SagaHistory>(sagaHistory, new QueryStatsInfo(stats.IndexEtag, stats.TotalResults));
            }
        }

        protected override SagaHistory ProcessResults(HttpRequestMessage request, QueryResult<SagaHistory>[] results)
        {
            return results.Select(p => p.Results).SingleOrDefault(x => x != null);
        }
    }
}