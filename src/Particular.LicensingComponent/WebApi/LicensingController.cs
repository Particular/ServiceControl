namespace Particular.LicensingComponent.WebApi
{
    using System.IO.Compression;
    using System.Text;
    using System.Text.Json;
    using System.Threading;
    using Contracts;
    using Microsoft.AspNetCore.Http;
    using Microsoft.AspNetCore.Mvc;
    using Microsoft.Net.Http.Headers;
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
        public async Task GetThroughputReportFile([FromQuery(Name = "spVersion")] string? spVersion, CancellationToken cancellationToken)
        {
            var reportStatus = await CanThroughputReportBeGenerated(cancellationToken);
            if (!reportStatus.ReportCanBeGenerated)
            {
                HttpContext.Response.StatusCode = StatusCodes.Status400BadRequest;
                HttpContext.Response.ContentType = "text/plain; charset=utf-8";

                await HttpContext.Response.WriteAsync($"Report cannot be generated – {reportStatus.Reason}", Encoding.UTF8, cancellationToken);
                return;
            }

            var report = await throughputCollector.GenerateThroughputReport(
                spVersion ?? "Unknown",
                null,
                cancellationToken);

            var fileName = $"{report.ReportData.CustomerName}.throughput-report-{report.ReportData.EndTime:yyyyMMdd-HHmmss}";

            HttpContext.Response.ContentType = "application/zip";
            HttpContext.Response.Headers[HeaderNames.ContentDisposition] = new ContentDispositionHeaderValue("attachment")
            {
                FileName = $"{fileName}.zip"
            }.ToString();

            using var archive = new ZipArchive(Response.BodyWriter.AsStream(), ZipArchiveMode.Create, leaveOpen: true);
            var entry = archive.CreateEntry($"{fileName}.json");
            await using var entryStream = entry.Open();
            await JsonSerializer.SerializeAsync(entryStream, report, SerializationOptions.IndentedWithNoEscaping, cancellationToken);
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
