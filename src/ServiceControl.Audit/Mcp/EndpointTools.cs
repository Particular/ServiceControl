#nullable enable

namespace ServiceControl.Audit.Mcp;

using System.ComponentModel;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using ModelContextProtocol.Server;
using Persistence;

[McpServerToolType, Description(
    "Read-only tools for discovering and inspecting NServiceBus endpoints.\n\n" +
    "Agent guidance:\n" +
    "1. Use GetKnownEndpoints to discover endpoint names before calling endpoint-specific tools.\n" +
    "2. Use GetEndpointAuditCounts to spot throughput trends, traffic spikes, or drops in activity."
)]
public class EndpointTools(IAuditDataStore store, ILogger<EndpointTools> logger)
{
    [McpServerTool(ReadOnly = true, Idempotent = true, Destructive = false, OpenWorld = false), Description(
        "This is a read-only tool for discovering what NServiceBus endpoints exist in the system. " +
        "Good for questions like: 'what endpoints do we have?', 'what services are running?', or 'list all endpoints'. " +
        "Returns all endpoints that have processed audit messages, including their name and host information. " +
        "This is a good starting point when you need an endpoint name for other tools like GetAuditMessagesByEndpoint or GetEndpointAuditCounts."
    )]
    public async Task<string> GetKnownEndpoints(CancellationToken cancellationToken = default)
    {
        logger.LogInformation("MCP GetKnownEndpoints invoked");

        var results = await store.QueryKnownEndpoints(cancellationToken);

        logger.LogInformation("MCP GetKnownEndpoints returned {Count} endpoints", results.QueryStats.TotalCount);

        return JsonSerializer.Serialize(new
        {
            results.QueryStats.TotalCount,
            results.Results
        }, McpJsonOptions.Default);
    }

    [McpServerTool(ReadOnly = true, Idempotent = true, Destructive = false, OpenWorld = false), Description(
        "This is a read-only tool for seeing daily message volume trends for a specific endpoint. " +
        "Good for questions like: 'how much traffic does Sales handle?', 'has throughput changed recently?', or 'show me message counts for this endpoint'. " +
        "Returns message counts per day, which helps identify throughput changes, traffic spikes, or drops in activity that might indicate problems. " +
        "You need an endpoint name — use GetKnownEndpoints first if you do not have one."
    )]
    public async Task<string> GetEndpointAuditCounts(
        [Description("The NServiceBus endpoint name, for example 'Sales' or 'Shipping.MessageHandler'.")] string endpointName,
        CancellationToken cancellationToken = default)
    {
        logger.LogInformation("MCP GetEndpointAuditCounts invoked (endpoint={EndpointName})", endpointName);

        var results = await store.QueryAuditCounts(endpointName, cancellationToken);

        logger.LogInformation("MCP GetEndpointAuditCounts returned {Count} entries for endpoint '{EndpointName}'", results.QueryStats.TotalCount, endpointName);

        return JsonSerializer.Serialize(new
        {
            results.QueryStats.TotalCount,
            results.Results
        }, McpJsonOptions.Default);
    }
}
