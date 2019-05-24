namespace ServiceControl.CompositeViews.Endpoints
{
    using System.Collections.Generic;
    using System.Threading.Tasks;
    using Messages;
    using Monitoring;
    using Nancy;

    class GetKnownEndpointsApi : ApiBase<NoInput, List<KnownEndpointsView>>
    {
        public EndpointInstanceMonitoring EndpointInstanceMonitoring { get; set; }

        public override Task<QueryResult<List<KnownEndpointsView>>> Query(Request request, NoInput input)
        {
            var result = EndpointInstanceMonitoring.GetKnownEndpoints();
            return Task.FromResult(new QueryResult<List<KnownEndpointsView>>(result, new QueryStatsInfo(string.Empty, result.Count)));
        }
    }
}