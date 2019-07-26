namespace ServiceControl.Monitoring
{
    using System.Linq;
    using System.Threading.Tasks;
    using CompositeViews.Messages;
    using Nancy;
    using Nancy.Extensions;

    class GetKnownEndpointsApi : ScatterGatherApi<NoInput, KnownEndpointsView[]>
    {
        EndpointInstanceMonitoring monitoring;

        public GetKnownEndpointsApi(EndpointInstanceMonitoring monitoring)
        {
            this.monitoring = monitoring;
        }

        public override Task<QueryResult<KnownEndpointsView[]>> LocalQuery(Request request, NoInput input)
        {
            return Task.FromResult(
                new QueryResult<KnownEndpointsView[]>(
                    monitoring.GetKnownEndpoints().ToArray(), 
                    QueryStatsInfo.Zero
                )
            );
        }

        protected override KnownEndpointsView[] ProcessResults(Request request, QueryResult<KnownEndpointsView[]>[] results)
        {
            return results.SelectMany(x => x.Results).DistinctBy(x => x.Id).ToArray();
        }
    }
}