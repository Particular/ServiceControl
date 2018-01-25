namespace ServiceControl.CompositeViews.Endpoints
{
    using System.Threading.Tasks;
    using Nancy;
    using ServiceControl.CompositeViews.Messages;
    using ServiceControl.Monitoring;

    public class GetKnownEndpointsApi : ScatterGatherApi<NoInput, KnownEndpointsView>
    {
        public EndpointInstanceMonitoring EndpointInstanceMonitoring { get; set; }

        public override async Task<QueryResult<KnownEndpointsView>> LocalQuery(Request request, NoInput input)
        {
            var result = EndpointInstanceMonitoring.GetKnownEndpoints();

            return Results(result);
        }
    }
}