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
            return await throughputCollector.GetThroughputSummary();
        }

        [Route("throughput/endpoints/update")]
        [HttpPost]
        public async Task<IActionResult> UpdateUserSelectionOnEndpointThroughput(List<EndpointThroughputSummary> endpointThroughputs)
        {
            await throughputCollector.UpdateUserSelectionOnEndpointThroughput(endpointThroughputs);
            return Ok();
        }

        [Route("throughput/report/available")]
        [HttpGet]
        public async Task<ReportGenerationState> CanThroughputReportBeGenerated()
        {
            return await throughputCollector.GetReportGenerationState();
        }

        [Route("throughput/report")]
        [HttpGet]
        public async Task<SignedReport> GetThroughputReport([FromQuery(Name = "prefix")] string? prefix, [FromQuery(Name = "masks")] string[]? masks, [FromQuery(Name = "spVersion")] string? spVersion)
        {
            return await throughputCollector.GenerateThroughputReport(prefix, masks, spVersion);
        }

        [Route("throughput/settings/info")]
        [HttpGet]
        public async Task<BrokerSettings> GetThroughputBrokerSettingsInformation()
        {
            return await throughputCollector.GetBrokerSettingsInformation();
        }

        [Route("throughput/settings/test")]
        [HttpGet]
        public async Task<BrokerSettingsTestResult> TestThroughputBrokerSettings()
        {
            return await throughputCollector.TestBrokerSettings();
        }

        readonly IThroughputCollector throughputCollector;
    }
}
