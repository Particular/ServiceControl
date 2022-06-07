namespace ServiceControl.Monitoring
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Net.Http;
    using System.Threading.Tasks;
    using CompositeViews.Messages;
    using Raven.Client;
    using ServiceBus.Management.Infrastructure.Settings;

    class GetKnownEndpointsApi : ScatterGatherApi<EndpointInstanceMonitoring, IList<KnownEndpointsView>>
    {
        public GetKnownEndpointsApi(IDocumentStore documentStore, RemoteInstanceSettings settings, Func<HttpClient> httpClientFactory) : base(documentStore, settings, httpClientFactory)
        {
        }

        protected override Task<QueryResult<IList<KnownEndpointsView>>> LocalQuery(HttpRequestMessage request, EndpointInstanceMonitoring input)
        {
            return Task.FromResult(
                new QueryResult<IList<KnownEndpointsView>>(
                    input.GetKnownEndpoints(),
                    new QueryStatsInfo(string.Empty, input.GetKnownEndpoints().Count)
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