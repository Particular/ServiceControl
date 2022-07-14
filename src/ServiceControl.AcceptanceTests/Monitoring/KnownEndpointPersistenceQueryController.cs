namespace ServiceControl.AcceptanceTests.Monitoring
{
    using System.Net;
    using System.Net.Http;
    using System.Threading.Tasks;
    using System.Web.Http;
    using ServiceControl.Monitoring;

    public class KnownEndpointPersistenceQueryController : ApiController
    {
        IMonitoringDataStore monitoringDataStore;

        internal KnownEndpointPersistenceQueryController(IMonitoringDataStore monitoringDataStore)
        {
            this.monitoringDataStore = monitoringDataStore;
        }

        [Route("test/knownendpoints/query")]
        [HttpGet]
        public async Task<HttpResponseMessage> GetKnownEndpoints()
        {
            var knownEndpoints = await monitoringDataStore.GetAllKnownEndpoints();
            return Request.CreateResponse(HttpStatusCode.OK, knownEndpoints);
        }
    }
}