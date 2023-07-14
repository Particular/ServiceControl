namespace ServiceControl.Monitoring
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Net.Http;
    using System.Threading.Tasks;
    using CompositeViews.Messages;
    using ServiceBus.Management.Infrastructure.Settings;
    using ServiceControl.Persistence;
    using ServiceControl.Persistence.Infrastructure;

    class GetKnownEndpointsApi : ScatterGatherApiNoInput<IEndpointInstanceMonitoring, IList<KnownEndpointsView>>
    {
        public GetKnownEndpointsApi(IEndpointInstanceMonitoring store, Settings settings, Func<HttpClient> httpClientFactory) : base(store, settings, httpClientFactory)
        {
        }

        protected override Task<QueryResult<IList<KnownEndpointsView>>> LocalQuery(HttpRequestMessage request)
        {
            var knownEndpoints = DataStore.GetKnownEndpoints();

            return Task.FromResult(
                new QueryResult<IList<KnownEndpointsView>>(
                    knownEndpoints,
                    new QueryStatsInfo(string.Empty, knownEndpoints.Count)
                )
            );
        }

        protected override IList<KnownEndpointsView> ProcessResults(HttpRequestMessage request, QueryResult<IList<KnownEndpointsView>>[] results)
        {
            return results.Where(p => p.Results != null).SelectMany(x => x.Results).Distinct(KnownEndpointsViewComparer.Instance).ToList();
        }

        class KnownEndpointsViewComparer : IEqualityComparer<KnownEndpointsView>
        {
            public bool Equals(KnownEndpointsView x, KnownEndpointsView y)
            {
                return y != null && x != null && x.Id.Equals(y.Id);
            }

            public int GetHashCode(KnownEndpointsView obj)
            {
                return obj.Id.GetHashCode();
            }

            public static KnownEndpointsViewComparer Instance = new KnownEndpointsViewComparer();
        }
    }
}