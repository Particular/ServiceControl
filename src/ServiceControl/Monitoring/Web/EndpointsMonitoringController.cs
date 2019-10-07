namespace ServiceControl.Monitoring
{
    using System;
    using System.Net;
    using System.Net.Http;
    using System.Threading.Tasks;
    using System.Web.Http;
    using System.Web.Http.Results;
    using CompositeViews.Endpoints;

    public class EndpointUpdateModel
    {
        public bool MonitorHeartbeat { get; set; }
    }

    public class EndpointsMonitoringController : ApiController
    {
        internal EndpointsMonitoringController(EndpointInstanceMonitoring monitoring, GetKnownEndpointsApi getKnownEndpointsApi)
        {
            this.getKnownEndpointsApi = getKnownEndpointsApi;
            endpointInstanceMonitoring = monitoring;
        }

        [Route("heartbeats/stats")]
        [HttpGet]
        public OkNegotiatedContentResult<EndpointMonitoringStats> HeartbeatStats() => Ok(endpointInstanceMonitoring.GetStats());

        [Route("endpoints")]
        [HttpGet]
        public OkNegotiatedContentResult<EndpointsView[]> Endpoints() => Ok(endpointInstanceMonitoring.GetEndpoints());

        [Route("endpoints/known")]
        [HttpGet]
        public Task<HttpResponseMessage> KnownEndpoints() => getKnownEndpointsApi.Execute(this, endpointInstanceMonitoring);

        [Route("endpoints/{endpointId}")]
        [HttpPatch]
        public async Task<StatusCodeResult> Foo(Guid endpointId, EndpointUpdateModel data)
        {
            if (data.MonitorHeartbeat)
            {
                await endpointInstanceMonitoring.EnableMonitoring(endpointId)
                    .ConfigureAwait(false);
            }
            else
            {
                await endpointInstanceMonitoring.DisableMonitoring(endpointId)
                    .ConfigureAwait(false);
            }

            return StatusCode(HttpStatusCode.Accepted);
        }

        readonly EndpointInstanceMonitoring endpointInstanceMonitoring;
        readonly GetKnownEndpointsApi getKnownEndpointsApi;
    }
}