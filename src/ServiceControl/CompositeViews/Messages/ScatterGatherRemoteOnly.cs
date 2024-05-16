namespace ServiceControl.CompositeViews.Messages
{
    using System.Net.Http;
    using System.Threading.Tasks;
    using Persistence.Infrastructure;
    using ServiceBus.Management.Infrastructure.Settings;

    public abstract class ScatterGatherRemoteOnly<TIn, TOut>(Settings settings, IHttpClientFactory httpClientFactory)
        : ScatterGatherApi<NoOpStore, TIn, TOut>(NoOpStore.Instance, settings, httpClientFactory)
        where TIn : ScatterGatherContext
        where TOut : class
    {
        protected sealed override Task<QueryResult<TOut>> LocalQuery(TIn input) => QueryResult<TOut>.Empty();
    }

    public sealed class NoOpStore
    {
        public static NoOpStore Instance => instance ??= new NoOpStore();

        static NoOpStore instance;
    }
}