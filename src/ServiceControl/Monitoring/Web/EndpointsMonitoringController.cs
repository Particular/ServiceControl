namespace ServiceControl.Monitoring
{
    using System;
    using System.Net;
    using System.Net.Http;
    using System.Threading.Tasks;
    using System.Web.Http;
    using System.Web.Http.Results;
    using CompositeViews.Endpoints;
    using Raven.Client;

    public class EndpointUpdateModel
    {
        public bool MonitorHeartbeat { get; set; }
    }

    public class EndpointsMonitoringController : ApiController
    {
        readonly IDocumentStore documentStore;

        internal EndpointsMonitoringController(EndpointInstanceMonitoring monitoring, GetKnownEndpointsApi getKnownEndpointsApi, IDocumentStore documentStore)
        {
            this.documentStore = documentStore;
            this.getKnownEndpointsApi = getKnownEndpointsApi;
            endpointInstanceMonitoring = monitoring;
        }

        [Route("heartbeats/stats")]
        [HttpGet]
        public OkNegotiatedContentResult<EndpointMonitoringStats> HeartbeatStats() => Ok(endpointInstanceMonitoring.GetStats());

        [Route("endpoints")]
        [HttpGet]
        public OkNegotiatedContentResult<EndpointsView[]> Endpoints() => Ok(endpointInstanceMonitoring.GetEndpoints());

        [Route("endpoints/{endpointId}")]
        [HttpDelete]
        public async Task<StatusCodeResult> DeleteEndpoint(Guid endpointId)
        {
            var removedFromCache = endpointInstanceMonitoring.RemoveEndpoint(endpointId);
            if (removedFromCache == null)
            {
                return StatusCode(HttpStatusCode.NotFound);
            }

            await DeletePersistedEndpoint(endpointId).ConfigureAwait(false);
            return StatusCode(HttpStatusCode.NoContent);
        }

        async Task DeletePersistedEndpoint(Guid endpointId)
        {
            using (var session = documentStore.OpenAsyncSession())
            {
                session.Delete<KnownEndpoint>(endpointId);
                await session.SaveChangesAsync().ConfigureAwait(false);    
            }
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