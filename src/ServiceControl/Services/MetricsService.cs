using System.Threading.Tasks;
using Grpc.Core;
using OpenTelemetry.Proto.Collector.Metrics.V1;

public class MetricsService : OpenTelemetry.Proto.Collector.Metrics.V1.MetricsService.MetricsServiceBase
{
    public override Task<ExportMetricsServiceResponse> Export(ExportMetricsServiceRequest request, ServerCallContext context)
    {
        return Task.FromResult(new ExportMetricsServiceResponse());
    }
}