#nullable enable

namespace ServiceControl.Audit.Mcp;

using System.ComponentModel;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using ModelContextProtocol.Server;
using Persistence;

[McpServerToolType]
public class EndpointTools(IAuditDataStore store)
{
    [McpServerTool, Description("Get a list of all known endpoints that have sent or received audit messages.")]
    public async Task<string> GetKnownEndpoints(CancellationToken cancellationToken = default)
    {
        var results = await store.QueryKnownEndpoints(cancellationToken);

        return JsonSerializer.Serialize(new
        {
            results.QueryStats.TotalCount,
            results.Results
        }, McpJsonOptions.Default);
    }

    [McpServerTool, Description("Get audit message counts per day for a specific endpoint. Useful for understanding message throughput.")]
    public async Task<string> GetEndpointAuditCounts(
        [Description("The name of the endpoint")] string endpointName,
        CancellationToken cancellationToken = default)
    {
        var results = await store.QueryAuditCounts(endpointName, cancellationToken);

        return JsonSerializer.Serialize(new
        {
            results.QueryStats.TotalCount,
            results.Results
        }, McpJsonOptions.Default);
    }
}
