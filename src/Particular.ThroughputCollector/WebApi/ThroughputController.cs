namespace Particular.ThroughputCollector.WebApi
{
    using System.Text.Json;
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
            await throughputCollector.UpdateUserIndicatorsOnEndpoints(endpointThroughputs);
            return Ok();
        }

        [Route("throughput/report/available")]
        [HttpGet]
        public async Task<ReportGenerationState> CanThroughputReportBeGenerated()
        {
            return await throughputCollector.GetReportGenerationState();
        }

        [Route("throughput/report/file")]
        [HttpGet]
        public async Task<FileContentResult> GetThroughputReportFile([FromQuery(Name = "masks")] string[]? masks, [FromQuery(Name = "spVersion")] string? spVersion)
        {
            var report = await throughputCollector.GenerateThroughputReport(masks, spVersion);
            var options = new JsonSerializerOptions()
            {
                WriteIndented = true,
                Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping
            };
            return File(JsonSerializer.SerializeToUtf8Bytes(report, options), "application/json", fileDownloadName: $"{report.ReportData.CustomerName}.throughput-report-{report.ReportData.EndTime:yyyyMMdd-HHmmss}.json");
        }

        [Route("throughput/settings/info")]
        [HttpGet]
        public async Task<ThroughputConnectionSettings> GetThroughputSettingsInformation()
        {
            return await throughputCollector.GetThroughputConnectionSettingsInformation();
        }

        [Route("throughput/settings/test")]
        [HttpGet]
        public async Task<ConnectionTestResults> TestThroughputConnectionSettings(CancellationToken cancellationToken)
        {
            return await throughputCollector.TestConnectionSettings(cancellationToken);
        }

        readonly IThroughputCollector throughputCollector;
    }
}
