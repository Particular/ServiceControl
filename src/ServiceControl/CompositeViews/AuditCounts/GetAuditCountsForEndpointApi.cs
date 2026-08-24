namespace ServiceControl.CompositeViews.MessageCounting
{
    using System.Collections.Generic;
    using System.Linq;
    using System.Net.Http;
    using Api.Contracts;
    using Messages;
    using Microsoft.AspNetCore.Http;
    using Microsoft.Extensions.Logging;
    using Persistence.Infrastructure;
    using ServiceBus.Management.Infrastructure.Settings;

    // The endpoint is included for consistency reasons but is actually not required here because the query
    // is forwarded to the remote instance. But this at least enforces us to declare the controller action
    // with the necessary parameter and not accessing the endpoint becomes an implementation details of the scatter
    // gather approach here.
    public record AuditCountsForEndpointContext(PagingInfo PagingInfo, string Endpoint) : ScatterGatherContext(PagingInfo);

    // The counts only ever live on an audit instance, so this instance has nothing of its own to add.
    public class GetAuditCountsForEndpointApi(
        Settings settings,
        IHttpClientFactory httpClientFactory,
        IHttpContextAccessor httpContextAccessor,
        ILogger<GetAuditCountsForEndpointApi> logger)
        : ScatterGatherRemoteOnly<AuditCountsForEndpointContext, IList<AuditCount>>(settings, httpClientFactory, httpContextAccessor, logger)
    {
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
