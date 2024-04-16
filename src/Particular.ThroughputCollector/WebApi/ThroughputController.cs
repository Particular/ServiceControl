namespace Particular.ThroughputCollector.WebApi
{
    using System.Text.Encodings.Web;
    using System.Text.Json;
    using Contracts;
    using Microsoft.AspNetCore.Mvc;

    [ApiController]
    [Route("api/throughput")]
    public class ThroughputController : ControllerBase
    {
        public ThroughputController(IThroughputCollector throughputCollector)
        {
            this.throughputCollector = throughputCollector;
        }

        [Route("endpoints")]
        [HttpGet]
        public async Task<List<EndpointThroughputSummary>> GetEndpointThroughput(CancellationToken token)
        {
            return await throughputCollector.GetThroughputSummary(token);
        }

        [Route("endpoints/update")]
        [HttpPost]
        public async Task<IActionResult> UpdateUserSelectionOnEndpointThroughput(List<EndpointThroughputSummary> endpointThroughputs, CancellationToken token)
        {
            await throughputCollector.UpdateUserIndicatorsOnEndpoints(endpointThroughputs);
            return Ok();
        }

        [Route("report/available")]
        [HttpGet]
        public async Task<ReportGenerationState> CanThroughputReportBeGenerated(CancellationToken token)
        {
            return await throughputCollector.GetReportGenerationState();
        }

        [Route("report/file")]
        [HttpGet]
        public async Task<IActionResult> GetThroughputReportFile(string[]? mask, CancellationToken token)
        {
            var reportStatus = await CanThroughputReportBeGenerated(token);
            if (reportStatus.ReportCanBeGenerated)
            {
                var report = await throughputCollector.GenerateThroughputReport(
                    mask ?? [],
                    Request.Headers.TryGetValue("Particular-ServicePulse-Version", out var value) ? value.ToString() : "Unknown",
                    token);

                var options = new JsonSerializerOptions
                {
                    WriteIndented = true,
                    Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping
                };
                return File(JsonSerializer.SerializeToUtf8Bytes(report, options), "application/json", fileDownloadName: $"{report.ReportData.CustomerName}.throughput-report-{report.ReportData.EndTime:yyyyMMdd-HHmmss}.json");
            }

            return BadRequest("Report cannot be generated.");
        }

        [Route("settings/info")]
        [HttpGet]
        public async Task<ThroughputConnectionSettings> GetThroughputSettingsInformation(CancellationToken token)
        {
            return await throughputCollector.GetThroughputConnectionSettingsInformation();
        }

        [Route("settings/test")]
        [HttpGet]
        public async Task<ConnectionTestResults> TestThroughputConnectionSettings(CancellationToken token) => await throughputCollector.TestConnectionSettings(token);

        readonly IThroughputCollector throughputCollector;
    }
}
