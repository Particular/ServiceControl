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
        public async Task<List<EndpointThroughputSummary>> GetEndpointThroughput([FromQuery(Name = "month")] int? month)
        {
            return await throughputCollector.GetThroughputSummary(month ?? DateTime.UtcNow.Month).ConfigureAwait(false);
        }

        [Route("throughput/report")]
        [HttpGet]
        public async Task<ThroughputReport> GetThroughputReport([FromQuery(Name = "month")] int? month)
        {
            return await throughputCollector.GenerateThroughputReport(month ?? DateTime.UtcNow.Month).ConfigureAwait(false);
        }

        [Route("throughput/settings")]
        [HttpGet]
        public async Task<BrokerSettings> GetThroughputBrokerSettings()
        {
            return await throughputCollector.GetBrokerSettings().ConfigureAwait(false);
        }

        [Route("throughput/endpoints/update")]
        [HttpPost]
        public async Task<IActionResult> UpdateUserSelectionOnEndpointThroughput(List<EndpointThroughputSummary> endpointThroughputs)
        {
            await throughputCollector.UpdateUserSelectionOnEndpointThroughput(endpointThroughputs).ConfigureAwait(false);
            return Ok();
        }

        [Route("throughput/settings/test")]
        [HttpGet]
        public async Task<BrokerSettingsTestResult> TestThroughputBrokerSettings()
        {
            return await throughputCollector.TestBrokerSettings().ConfigureAwait(false);
        }

        readonly IThroughputCollector throughputCollector;
    }
}
