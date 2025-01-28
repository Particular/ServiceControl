using System;
using System.Linq;
using System.Threading.Tasks;
using Grpc.Core;
using OpenTelemetry.Proto.Collector.Trace.V1;
using OpenTelemetry.Proto.Trace.V1;
using ServiceControl.Operations;
using ServiceControl.Persistence;

public class TraceService(IEndpointInstanceMonitoring endpointInstanceMonitoring) : OpenTelemetry.Proto.Collector.Trace.V1.TraceService.TraceServiceBase
{
    public override async Task<ExportTraceServiceResponse> Export(ExportTraceServiceRequest request, ServerCallContext context)
    {
        foreach (var resourceSpan in request.ResourceSpans)
        {
            foreach (var scopeSpan in resourceSpan.ScopeSpans)
            {
                foreach (var span in scopeSpan.Spans)
                {
                    switch (span.Name)
                    {
                        case "EndpointStarted":
                            await endpointInstanceMonitoring.EndpointDetected(GetEndpointDetails(span));

                            Console.WriteLine($"{span.Name}: {span.Attributes.Single(kv => kv.Key == "nservicebus.endpoint.name").Value.StringValue} - {DateTimeOffset.FromUnixTimeMilliseconds((long)Math.Round(span.EndTimeUnixNano / (double)1000000))}");
                            break;
                        case "EndpointHeartbeat":

                            var heartbeatAt = DateTimeOffset.FromUnixTimeMilliseconds((long)Math.Round(span.EndTimeUnixNano / (double)1000000));
                            var endpointDetails = GetEndpointDetails(span);
                            var endpointInstanceId = new EndpointInstanceId(endpointDetails.Name, endpointDetails.Host, endpointDetails.HostId);

                            endpointInstanceMonitoring.RecordHeartbeat(endpointInstanceId, heartbeatAt.DateTime);

                            Console.WriteLine($"{span.Name}: {span.Attributes.Single(kv => kv.Key == "nservicebus.endpoint.name").Value.StringValue} - {DateTimeOffset.FromUnixTimeMilliseconds((long)Math.Round(span.EndTimeUnixNano / (double)1000000))}");
                            break;
                        case "EndpointStopped":
                            //TODO - add new method
                            //await endpointInstanceMonitoring.EndpointStopped(GetEndpointDetails(span));
                            Console.WriteLine($"{span.Name}: {span.Attributes.Single(kv => kv.Key == "nservicebus.endpoint.name").Value.StringValue} - {DateTimeOffset.FromUnixTimeMilliseconds((long)Math.Round(span.EndTimeUnixNano / (double)1000000))}");
                            break;
                        default:
                            break;
                    }
                }
            }
        }

        return new ExportTraceServiceResponse();
    }

    EndpointDetails GetEndpointDetails(Span span)
    {
        var endpointName = span.Attributes.Single(kv => kv.Key == "nservicebus.endpoint.name").Value.StringValue;
        var host = span.Attributes.Single(kv => kv.Key == "nservicebus.host.name").Value.StringValue;
        var hostId = span.Attributes.Single(kv => kv.Key == "nservicebus.host.id").Value.StringValue;

        return new EndpointDetails
        {
            Name = endpointName,
            Host = host,
            HostId = Guid.Parse(hostId)
        };
    }
}