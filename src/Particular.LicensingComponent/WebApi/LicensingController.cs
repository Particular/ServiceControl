namespace Particular.LicensingComponent.WebApi
{
    using System.Text.Json;
    using System.Threading;
    using Contracts;
    using Microsoft.AspNetCore.Mvc;
    using Particular.LicensingComponent.Report;

    [ApiController]
    [Route("api/licensing")]
    public class LicensingController : ControllerBase
    {
        public LicensingController(IThroughputCollector throughputCollector)
        {
            this.throughputCollector = throughputCollector;
        }

        [Route("endpoints")]
        [HttpGet]
        public async Task<List<EndpointThroughputSummary>> GetEndpointThroughput(CancellationToken cancellationToken)
        {
            return await throughputCollector.GetThroughputSummary(cancellationToken);
        }

        [Route("endpoints/update")]
        [HttpPost]
        public async Task<IActionResult> UpdateUserSelectionOnEndpointThroughput(List<UpdateUserIndicator> updateUserIndicators, CancellationToken cancellationToken)
        {
            await throughputCollector.UpdateUserIndicatorsOnEndpoints(updateUserIndicators, cancellationToken);
            return Ok();
        }

        [Route("report/available")]
        [HttpGet]
        public async Task<ReportGenerationState> CanThroughputReportBeGenerated(CancellationToken cancellationToken)
        {
            return await throughputCollector.GetReportGenerationState(cancellationToken);
        }

        [Route("report/file")]
        [HttpGet]
        public async Task<IActionResult> GetThroughputReportFile([FromQuery(Name = "spVersion")] string? spVersion, CancellationToken cancellationToken)
        {
            var reportStatus = await CanThroughputReportBeGenerated(cancellationToken);
            if (reportStatus.ReportCanBeGenerated)
            {
                var report = await throughputCollector.GenerateThroughputReport(
                    spVersion ?? "Unknown",
                    null,
                    cancellationToken);

                return File(JsonSerializer.SerializeToUtf8Bytes(report, SerializationOptions.IndentedWithNoEscaping), "application/json", fileDownloadName: $"{report.ReportData.CustomerName}.throughput-report-{report.ReportData.EndTime:yyyyMMdd-HHmmss}.json");
            }

            return BadRequest($"Report cannot be generated - {reportStatus.Reason}");
        }

        [Route("settings/info")]
        [HttpGet]
        public async Task<ThroughputConnectionSettings> GetThroughputSettingsInformation(CancellationToken cancellationToken)
        {
            return await throughputCollector.GetThroughputConnectionSettingsInformation(cancellationToken);
        }

        [Route("settings/test")]
        [HttpGet]
        public async Task<ConnectionTestResults> TestThroughputConnectionSettings(CancellationToken cancellationToken) => await throughputCollector.TestConnectionSettings(cancellationToken);

        [Route("settings/masks")]
        [HttpGet]
        public async Task<List<string>> GetMasks(CancellationToken cancellationToken)
        {
            return await throughputCollector.GetReportMasks(cancellationToken);
        }

        [Route("settings/masks/update")]
        [HttpPost]
        public async Task<IActionResult> UpdateMasks(List<string> updateMasks, CancellationToken cancellationToken)
        {
            await throughputCollector.UpdateReportMasks(updateMasks, cancellationToken);
            return Ok();
        }

        readonly IThroughputCollector throughputCollector;
    }
}