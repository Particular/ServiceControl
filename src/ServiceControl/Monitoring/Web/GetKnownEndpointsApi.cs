﻿namespace ServiceControl.Monitoring
{
    using System.Collections.Generic;
    using System.Linq;
    using System.Net.Http;
    using System.Threading.Tasks;
    using CompositeViews.Messages;
    using ServiceBus.Management.Infrastructure.Settings;
    using ServiceControl.Persistence;
    using ServiceControl.Persistence.Infrastructure;

    public class GetKnownEndpointsApi(IEndpointInstanceMonitoring store, Settings settings, IHttpClientFactory httpClientFactory) : ScatterGatherApi<IEndpointInstanceMonitoring, ScatterGatherContext, IList<KnownEndpointsView>>(store, settings, httpClientFactory)
    {
        protected override Task<QueryResult<IList<KnownEndpointsView>>> LocalQuery(ScatterGatherContext input)
        {
            var knownEndpoints = DataStore.GetKnownEndpoints();

            return Task.FromResult(
                new QueryResult<IList<KnownEndpointsView>>(
                    knownEndpoints,
                    new QueryStatsInfo(string.Empty, knownEndpoints.Count, isStale: false)
                )
            );
        }

        protected override IList<KnownEndpointsView> ProcessResults(ScatterGatherContext input, QueryResult<IList<KnownEndpointsView>>[] results) => results.Where(p => p.Results != null).SelectMany(x => x.Results).Distinct(KnownEndpointsViewComparer.Instance).ToList();

        class KnownEndpointsViewComparer : IEqualityComparer<KnownEndpointsView>
        {
            public bool Equals(KnownEndpointsView x, KnownEndpointsView y) => y != null && x != null && x.Id.Equals(y.Id);

            public int GetHashCode(KnownEndpointsView obj) => obj.Id.GetHashCode();

            public static KnownEndpointsViewComparer Instance = new KnownEndpointsViewComparer();
        }
    }
}