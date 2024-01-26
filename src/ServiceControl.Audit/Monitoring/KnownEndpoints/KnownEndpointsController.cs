namespace ServiceControl.Audit.Monitoring
{
    using System.Net.Http;
    using System.Threading.Tasks;
    using Microsoft.AspNetCore.Mvc;

    [ApiController]
    [Route("api")]
    public class KnownEndpointsController : ControllerBase
    {
        public KnownEndpointsController(GetKnownEndpointsApi knownEndpointsApi) => getKnownEndpointsApi = knownEndpointsApi;

        [Route("endpoints/known")]
        [HttpGet]
        public Task<HttpResponseMessage> GetAll() => getKnownEndpointsApi.Execute(this);

        readonly GetKnownEndpointsApi getKnownEndpointsApi;
    }
}