using System.Threading.Tasks;
using Grpc.Core;
using OpenTelemetry.Proto.Collector.Logs.V1;

public class LogsService : OpenTelemetry.Proto.Collector.Logs.V1.LogsService.LogsServiceBase
{
    public override Task<ExportLogsServiceResponse> Export(ExportLogsServiceRequest request, ServerCallContext context) => Task.FromResult(new ExportLogsServiceResponse());
}