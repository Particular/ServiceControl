#nullable enable

namespace ServiceControl.Audit.Mcp;

using System.ComponentModel;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Auditing;
using Microsoft.Extensions.Logging;
using ModelContextProtocol.Server;
using Monitoring;
using Persistence;
using ServiceControl.Infrastructure.Mcp;

[McpServerToolType, Description(
    "Read-only tools for discovering and inspecting NServiceBus endpoints.\n\n" +
    "Agent guidance:\n" +
    "1. Use GetKnownEndpoints to discover endpoint names before calling endpoint-specific tools.\n" +
    "2. Use GetEndpointAuditCounts to spot throughput trends, traffic spikes, or drops in activity."
)]
public class EndpointTools(IAuditDataStore store, ILogger<EndpointTools> logger)
{
    [McpServerTool(ReadOnly = true, Idempotent = true, Destructive = false, OpenWorld = false, UseStructuredContent = true), Description(
        "List all known endpoints that have sent or received audit messages. " +
        "Use this as a starting point to discover available endpoints before exploring their activity. " +
        "Read-only."
    )]
    public async Task<McpCollectionResult<KnownEndpointsView>> GetKnownEndpoints(CancellationToken cancellationToken = default)
    {
        logger.LogInformation("MCP GetKnownEndpoints invoked");

        var results = await store.QueryKnownEndpoints(cancellationToken);

        logger.LogInformation("MCP GetKnownEndpoints returned {Count} endpoints", results.QueryStats.TotalCount);

        return new McpCollectionResult<KnownEndpointsView>
        {
            TotalCount = (int)results.QueryStats.TotalCount,
            Results = results.Results.ToArray()
        };
    }

    [McpServerTool(ReadOnly = true, Idempotent = true, Destructive = false, OpenWorld = false, UseStructuredContent = true), Description(
        "Retrieve daily audit-message counts for a specific endpoint. " +
        "Use this when checking throughput or activity trends for one endpoint. " +
        "Prefer GetKnownEndpoints when you do not already know the endpoint name. " +
        "Read-only."
    )]
    public async Task<McpCollectionResult<AuditCount>> GetEndpointAuditCounts(
        [Description("The NServiceBus endpoint name whose audit activity should be counted. Use values obtained from GetKnownEndpoints.")] string endpointName,
        CancellationToken cancellationToken = default)
    {
        logger.LogInformation("MCP GetEndpointAuditCounts invoked (endpoint={EndpointName})", endpointName);

        var results = await store.QueryAuditCounts(endpointName, cancellationToken);

        logger.LogInformation("MCP GetEndpointAuditCounts returned {Count} entries for endpoint '{EndpointName}'", results.QueryStats.TotalCount, endpointName);

        return new McpCollectionResult<AuditCount>
        {
            TotalCount = (int)results.QueryStats.TotalCount,
            Results = results.Results.ToArray()
        };
    }
}
