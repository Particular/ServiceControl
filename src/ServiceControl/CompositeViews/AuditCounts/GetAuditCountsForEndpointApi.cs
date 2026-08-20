namespace ServiceControl.CompositeViews.MessageCounting
{
    using System.Collections.Generic;
    using System.Linq;
    using System.Threading;
    using System.Threading.Tasks;
    using System.Net.Http;
    using Api.Contracts;
    using Messages;
    using Microsoft.AspNetCore.Http;
    using Microsoft.Extensions.Logging;
    using Persistence;
    using Persistence.Infrastructure;
    using ServiceBus.Management.Infrastructure.Settings;

    public record AuditCountsForEndpointContext(PagingInfo PagingInfo, string Endpoint) : ScatterGatherContext(PagingInfo);

    public class GetAuditCountsForEndpointApi(
        IAuditCountsDataStore dataStore,
        Settings settings,
        IHttpClientFactory httpClientFactory,
        IHttpContextAccessor httpContextAccessor,
        ILogger<GetAuditCountsForEndpointApi> logger)
        : ScatterGatherApi<IAuditCountsDataStore, AuditCountsForEndpointContext, IList<AuditCount>>(dataStore, settings, httpClientFactory, httpContextAccessor, logger)
    {
        protected override Task<QueryResult<IList<AuditCount>>> LocalQuery(AuditCountsForEndpointContext input, CancellationToken cancellationToken = default) =>
            DataStore.QueryAuditCounts(input.Endpoint, cancellationToken);

        protected override IList<AuditCount> ProcessResults(AuditCountsForEndpointContext input, QueryResult<IList<AuditCount>>[] results) =>
            results.Where(r => r.Results is not null)
                .SelectMany(r => r.Results)
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
