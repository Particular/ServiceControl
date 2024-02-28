namespace Particular.ThroughputCollector.WebApi
{
    using Microsoft.AspNetCore.Mvc;
    using Particular.ThroughputCollector.Contracts;

    [ApiController]
    [Route("api")]
    public class ThroughputController : ControllerBase
    {
        public ThroughputController(IThroughputCollector throughputCollector)
        {
            this.throughputCollector = throughputCollector;
        }

        [Route("throughput/endpoints")]
        [HttpGet]
        public async Task<List<EndpointThroughputSummary>> GetEndpointThroughput()
        {
            return await Task.FromResult(new List<EndpointThroughputSummary>()).ConfigureAwait(false);
        }

        [Route("throughput/report")]
        [HttpGet]
        public async Task<string> GetThroughputReport()
        {
            return await Task.FromResult("OK").ConfigureAwait(false);
        }

        [Route("throughput/settings")]
        [HttpGet]
        public async Task<string> GetThroughputBrokerSettings()
        {
            return await Task.FromResult("OK").ConfigureAwait(false);
        }

        //[Route("throughput/endpoints/update")]
        //[HttpPost]
        //public async Task<IActionResult> ToggleEmailNotifications(List<EndpointThroughputSummary> endpointThroughputs)
        //{
        //    return Ok(boo);
        //}

        [Route("throughput/settings/test")]
        [HttpGet]
        public async Task<string> TestThroughputBrokerSettings()
        {
            return await Task.FromResult("OK").ConfigureAwait(false);
        }

#pragma warning disable IDE0052 // Remove unread private members
        readonly IThroughputCollector throughputCollector;
#pragma warning restore IDE0052 // Remove unread private members
    }
}
