namespace ServiceControl.CompositeViews.MessageCounting
{
    using System.Collections.Generic;
    using System.Linq;
    using System.Net.Http;
    using System.Threading.Tasks;
    using Api.Contracts;
    using Messages;
    using Microsoft.Extensions.Logging;
    using Persistence;
    using Persistence.Infrastructure;
    using ServiceBus.Management.Infrastructure.Settings;

    // The endpoint is included for consistency reasons but is actually not required here because the query
    // is forwarded to the remote instance. But this at least enforces us to declare the controller action
    // with the necessary parameter and not accessing the endpoint becomes an implementation details of the scatter
    // gather approach here.
    public record AuditCountsForEndpointContext(PagingInfo PagingInfo, string Endpoint) : ScatterGatherContext(PagingInfo);

    public class GetAuditCountsForEndpointApi(
        IErrorMessageDataStore dataStore,
        Settings settings,
        IHttpClientFactory httpClientFactory,
        ILogger<GetAuditCountsForEndpointApi> logger)
        : ScatterGatherApi<IErrorMessageDataStore, AuditCountsForEndpointContext, IList<AuditCount>>(dataStore, settings, httpClientFactory, logger)
    {
        static readonly IList<AuditCount> Empty = new List<AuditCount>(0).AsReadOnly();

        // Will never be implemented on the primary instance
        protected override Task<QueryResult<IList<AuditCount>>> LocalQuery(AuditCountsForEndpointContext input) =>
            Task.FromResult(new QueryResult<IList<AuditCount>>(Empty, QueryStatsInfo.Zero));

        protected override IList<AuditCount> ProcessResults(AuditCountsForEndpointContext input, QueryResult<IList<AuditCount>>[] results) =>
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
