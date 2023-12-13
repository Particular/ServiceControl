namespace ServiceControl.SagaAudit
{
    using System;
    using System.Linq;
    using System.Net.Http;
    using System.Threading.Tasks;
    using CompositeViews.Messages;
    using Microsoft.AspNetCore.Http;
    using ServiceBus.Management.Infrastructure.Settings;
    using ServiceControl.Persistence;
    using ServiceControl.Persistence.Infrastructure;

    public record SagaByIdContext(PagingInfo PagingInfo, Guid SagaId) : ScatterGatherContext(PagingInfo);

    class GetSagaByIdApi : ScatterGatherApi<ISagaAuditDataStore, SagaByIdContext, SagaHistory>
    {
        public GetSagaByIdApi(ISagaAuditDataStore dataStore, Settings settings, IHttpClientFactory httpClientFactory, IHttpContextAccessor httpContextAccessor) : base(dataStore, settings, httpClientFactory, httpContextAccessor)
        {
        }

        protected override async Task<QueryResult<SagaHistory>> LocalQuery(SagaByIdContext input)
        {
            var result = await DataStore.GetSagaById(input.SagaId);
            return result;
        }

        protected override SagaHistory ProcessResults(SagaByIdContext input, QueryResult<SagaHistory>[] results)
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