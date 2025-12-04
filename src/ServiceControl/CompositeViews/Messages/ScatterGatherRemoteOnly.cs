namespace ServiceControl.CompositeViews.Messages
{
    using System.Net.Http;
    using System.Threading.Tasks;
    using Microsoft.Extensions.Logging;
    using Persistence.Infrastructure;
    using ServiceBus.Management.Infrastructure.Settings;

    public abstract class ScatterGatherRemoteOnly<TIn, TOut>(Settings settings, IHttpClientFactory httpClientFactory, ILogger logger)
        : ScatterGatherApi<NoOpStore, TIn, TOut>(NoOpStore.Instance, settings, httpClientFactory, logger)
        where TIn : ScatterGatherContext
        where TOut : class
    {
        protected sealed override Task<QueryResult<TOut>> LocalQuery(TIn input) => QueryResult<TOut>.Empty();
    }

    public sealed class NoOpStore
    {
        public static NoOpStore Instance => field ??= new NoOpStore();
    }
}