namespace ServiceControl.Mcp;

using System.ComponentModel;
using System.Text.Json;
using System.Threading.Tasks;
using CompositeViews.Messages;
using ModelContextProtocol.Server;
using Monitoring;
using Persistence;
using Persistence.Infrastructure;

[McpServerToolType]
static class EndpointTools
{
    [McpServerTool(Name = "get_endpoints"), Description("List all known endpoints with their heartbeat and monitoring status.")]
    public static string GetEndpoints(IEndpointInstanceMonitoring monitoring)
    {
        var endpoints = monitoring.GetEndpoints();
        return JsonSerializer.Serialize(endpoints);
    }

    [McpServerTool(Name = "get_heartbeat_stats"), Description("Get a summary of endpoint heartbeat statistics including active and failing counts.")]
    public static string GetHeartbeatStats(IEndpointInstanceMonitoring monitoring)
    {
        var stats = monitoring.GetStats();
        return JsonSerializer.Serialize(stats);
    }

    [McpServerTool(Name = "get_known_endpoints"), Description("List all known endpoints including those that have never sent a heartbeat.")]
    public static async Task<string> GetKnownEndpoints(
        GetKnownEndpointsApi api,
        [Description("Page number (1-based).")] int page = 1,
        [Description("Results per page (default 50).")] int pageSize = 50)
    {
        var pagingInfo = new PagingInfo(page, pageSize);
        var result = await api.Execute(new ScatterGatherContext(pagingInfo), "/api/endpoints/known");
        return JsonSerializer.Serialize(result.Results);
    }
}
