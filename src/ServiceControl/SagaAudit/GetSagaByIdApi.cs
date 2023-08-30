namespace ServiceControl.SagaAudit
{
    using System;
    using System.Linq;
    using System.Net.Http;
    using System.Threading.Tasks;
    using CompositeViews.Messages;
    using ServiceBus.Management.Infrastructure.Settings;
    using ServiceControl.Persistence;
    using ServiceControl.Persistence.Infrastructure;

    class GetSagaByIdApi : ScatterGatherApi<ISagaAuditDataStore, Guid, SagaHistory>
    {
        public GetSagaByIdApi(ISagaAuditDataStore store, Settings settings, Func<HttpClient> httpClientFactory) : base(store, settings, httpClientFactory)
        {
        }

        protected override async Task<QueryResult<SagaHistory>> LocalQuery(HttpRequestMessage request, Guid input)
        {
            var result = await DataStore.GetSagaById(input);
            return result;
        }

        protected override SagaHistory ProcessResults(HttpRequestMessage request, QueryResult<SagaHistory>[] results)
        {
            var nonEmptyCount = results.Count(x => x.Results != null);
            if (nonEmptyCount == 0)
            {
                return null;
            }

            if (nonEmptyCount == 1)
            {
                return results.Select(p => p.Results).First(x => x != null);
            }

            var firstResult = results.Select(p => p.Results).First(x => x != null);
            var mergedChanges = results.Select(p => p.Results)
                    .Where(x => x != null)
                    .SelectMany(x => x.Changes)
                    .OrderByDescending(x => x.FinishTime)
                    .ToList();
            firstResult.Changes = mergedChanges;
            return firstResult;
        }
    }
}