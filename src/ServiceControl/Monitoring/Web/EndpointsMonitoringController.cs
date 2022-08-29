namespace ServiceControl.Monitoring
{
    using System;
    using System.Net;
    using System.Net.Http;
    using System.Threading.Tasks;
    using System.Web.Http;
    using System.Web.Http.Results;
    using CompositeViews.Endpoints;
    using ServiceControl.Persistence;

    public class EndpointUpdateModel
    {
        public bool MonitorHeartbeat { get; set; }
    }

    class EndpointsMonitoringController : ApiController
    {
        readonly IMonitoringDataStore monitoringDataStore;

        public EndpointsMonitoringController(EndpointInstanceMonitoring monitoring, GetKnownEndpointsApi getKnownEndpointsApi, IMonitoringDataStore monitoringDataStore)
        {
            this.getKnownEndpointsApi = getKnownEndpointsApi;
            this.monitoringDataStore = monitoringDataStore;
            endpointInstanceMonitoring = monitoring;
        }

        [Route("heartbeats/stats")]
        [HttpGet]
        public OkNegotiatedContentResult<EndpointMonitoringStats> HeartbeatStats() => Ok(endpointInstanceMonitoring.GetStats());

        [Route("endpoints")]
        [HttpGet]
        public OkNegotiatedContentResult<EndpointsView[]> Endpoints() => Ok(endpointInstanceMonitoring.GetEndpoints());

        //Added as a way for SP to check if the DELETE operation is supported by the SC API
        [Route("endpoints")]
        [HttpOptions]
        public HttpResponseMessage GetSupportedOperations()
        {
            var response = new HttpResponseMessage(HttpStatusCode.NoContent)
            {
                Content = new ByteArrayContent(new byte[] { }) //need to force empty content to avoid null reference when adding headers below :(
            };

            response.Content.Headers.Allow.Add("GET");
            response.Content.Headers.Allow.Add("DELETE");
            response.Content.Headers.Allow.Add("PATCH");
            response.Content.Headers.Add("Access-Control-Expose-Headers", "Allow");
            return response;
        }

        [Route("endpoints/{endpointId}")]
        [HttpDelete]
        public async Task<StatusCodeResult> DeleteEndpoint(Guid endpointId)
        {
            if (!endpointInstanceMonitoring.HasEndpoint(endpointId))
            {
                return StatusCode(HttpStatusCode.NotFound);
            }

            await monitoringDataStore.Delete(endpointId).ConfigureAwait(false);

            endpointInstanceMonitoring.RemoveEndpoint(endpointId);
            return StatusCode(HttpStatusCode.NoContent);
        }

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
