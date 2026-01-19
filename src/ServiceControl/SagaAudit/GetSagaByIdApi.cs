namespace ServiceControl.SagaAudit
{
    using System;
    using System.Linq;
    using System.Net.Http;
    using CompositeViews.Messages;
    using Microsoft.AspNetCore.Http;
    using Microsoft.Extensions.Logging;
    using Persistence.Infrastructure;
    using ServiceBus.Management.Infrastructure.Settings;

    public record SagaByIdContext(PagingInfo PagingInfo, Guid SagaId) : ScatterGatherContext(PagingInfo);

    public class GetSagaByIdApi(Settings settings, IHttpClientFactory httpClientFactory, IHttpContextAccessor httpContextAccessor, ILogger<GetSagaByIdApi> logger)
        : ScatterGatherRemoteOnly<SagaByIdContext, SagaHistory>(settings, httpClientFactory, httpContextAccessor, logger)
    {
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