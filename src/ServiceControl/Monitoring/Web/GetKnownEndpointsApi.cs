namespace ServiceControl.Monitoring
{
    using System.Collections.Generic;
    using System.Linq;
    using System.Threading.Tasks;
    using CompositeViews.Messages;
    using Nancy;
    using Nancy.Extensions;

    class GetKnownEndpointsApi : ScatterGatherApi<NoInput, IList<KnownEndpointsView>>
    {
        EndpointInstanceMonitoring monitoring;

        public GetKnownEndpointsApi(EndpointInstanceMonitoring monitoring)
        {
            this.monitoring = monitoring;
        }

        public override Task<QueryResult<IList<KnownEndpointsView>>> LocalQuery(Request request, NoInput input)
        {
            var result = monitoring.GetKnownEndpoints();
            
            return Task.FromResult(
                new QueryResult<IList<KnownEndpointsView>>(
                    result, 
                    new QueryStatsInfo(string.Empty, result.Count)
                )
            );
        }

        protected override IList<KnownEndpointsView> ProcessResults(Request request, QueryResult<IList<KnownEndpointsView>>[] results)
        {
            return results.Where(p => p.Results != null).SelectMany(x => x.Results).DistinctBy(x => x.Id).ToList();
        }
    }
}