namespace ServiceControl.CompositeViews.Endpoints
{
    using System.Collections.Generic;
    using System.Linq;
    using System.Threading.Tasks;
    using Nancy;
    using Nancy.Extensions;
    using ServiceControl.CompositeViews.Messages;
    using ServiceControl.Monitoring;

    public class GetKnownEndpointsApi : ScatterGatherApi<NoInput, List<KnownEndpointsView>>
    {
        public MonitoringDataPersister Persister { get; set; }

        public override Task<QueryResult<List<KnownEndpointsView>>> LocalQuery(Request request, NoInput input)
        {
            var result = Persister.GetKnownEndpoints();
            return Task.FromResult(new QueryResult<List<KnownEndpointsView>>(result, new QueryStatsInfo(string.Empty, result.Count)));
        }

        protected override List<KnownEndpointsView> ProcessResults(Request request, QueryResult<List<KnownEndpointsView>>[] results)
        {
            return results.Where(p => p.Results != null).SelectMany(p => p.Results).DistinctBy(e => e.Id).ToList();
        }

        protected override QueryStatsInfo AggregateStats(IEnumerable<QueryResult<List<KnownEndpointsView>>> results, List<KnownEndpointsView> processedResults)
        {
            var aggregatedStats = base.AggregateStats(results, processedResults);

            return new QueryStatsInfo(aggregatedStats.ETag, processedResults.Count);
        }
    }
}