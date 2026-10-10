namespace ServiceControl.CompositeViews.Messages
{
    using System.Collections.Generic;
    using System.Net.Http;
    using System.Threading;
    using System.Threading.Tasks;
    using Microsoft.AspNetCore.Http;
    using Microsoft.Extensions.Logging;
    using Persistence.Infrastructure;
    using ServiceBus.Management.Infrastructure.Settings;

    public abstract class ScatterGatherRemoteOnly<TIn, TOut>(Settings settings, IHttpClientFactory httpClientFactory, IHttpContextAccessor httpContextAccessor, ILogger logger)
        : ScatterGatherApi<NoOpStore, TIn, TOut>(NoOpStore.Instance, settings, httpClientFactory, httpContextAccessor, logger)
        where TIn : ScatterGatherContext
        where TOut : class
    {
        protected sealed override Task<QueryResult<TOut>> LocalQuery(TIn input, CancellationToken cancellationToken = default) => QueryResult<TOut>.Empty();

        protected sealed override bool LocalInstanceParticipates => false;

        protected sealed override QueryStatsInfo AggregateStats(TIn input, IEnumerable<QueryResult<TOut>> results, TOut processedResults) =>
            AggregateStatsFromRemotesOnly(results);
    }

    public sealed class NoOpStore
    {
        public static NoOpStore Instance => field ??= new NoOpStore();
    }
}