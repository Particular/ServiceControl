namespace ServiceControl.Monitoring.Mcp;

using System.ComponentModel;
using System.Text.Json;
using ModelContextProtocol.Server;
using Infrastructure.Api;

[McpServerToolType]
static class MonitoringTools
{
    [McpServerTool(Name = "get_monitored_endpoints"), Description("List all monitored endpoints with their current performance metrics (throughput, processing time, critical time, retry rate, queue length).")]
    public static string GetMonitoredEndpoints(
        IEndpointMetricsApi api,
        [Description("Number of history periods to include (default is the last period).")] int? history = null)
    {
        var endpoints = api.GetAllEndpointsMetrics(history);
        return JsonSerializer.Serialize(endpoints);
    }

    [McpServerTool(Name = "get_monitored_endpoint"), Description("Get detailed performance metrics for a specific monitored endpoint.")]
    public static string GetMonitoredEndpoint(
        IEndpointMetricsApi api,
        [Description("The name of the endpoint to retrieve metrics for.")] string endpointName,
        [Description("Number of history periods to include (default is the last period).")] int? history = null)
    {
        var details = api.GetSingleEndpointMetrics(endpointName, history);
        return details == null
            ? $"Endpoint '{endpointName}' not found."
            : JsonSerializer.Serialize(details);
    }

    [McpServerTool(Name = "get_disconnected_endpoint_count"), Description("Get the count of monitored endpoints that have stopped sending metrics (disconnected).")]
    public static string GetDisconnectedEndpointCount(IEndpointMetricsApi api)
    {
        var count = api.DisconnectedEndpointCount();
        return JsonSerializer.Serialize(new { disconnectedCount = count });
    }
}
