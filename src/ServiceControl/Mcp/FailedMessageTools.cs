#nullable enable

namespace ServiceControl.Mcp;

using System.ComponentModel;
using System.Text.Json;
using System.Threading.Tasks;
using MessageFailures.Api;
using Microsoft.Extensions.Logging;
using ModelContextProtocol.Server;
using Persistence;
using Persistence.Infrastructure;

[McpServerToolType, Description(
    "Read-only tools for investigating failed messages.\n\n" +
    "Agent guidance:\n" +
    "1. Start with GetErrorsSummary to get a quick health check of failure counts by status.\n" +
    "2. Use GetFailureGroups (from FailureGroupTools) to see failures grouped by root cause before drilling into individual messages.\n" +
    "3. Use GetFailedMessages for broad listing, or GetFailedMessagesByEndpoint when you already know the endpoint.\n" +
    "4. Use GetFailedMessageById for full details including all processing attempts, or GetFailedMessageLastAttempt for just the most recent failure.\n" +
    "5. Keep page=1 unless the user asks for more results.\n" +
    "6. Only change sorting when the user explicitly asks for it."
)]
public class FailedMessageTools(IErrorMessageDataStore store, ILogger<FailedMessageTools> logger)
{
    [McpServerTool(ReadOnly = true, Idempotent = true, Destructive = false, OpenWorld = false), Description(
        "Retrieve failed messages for investigation. " +
        "Use this when exploring recent failures or narrowing down failures by queue, status, or time range. " +
        "Prefer GetFailureGroups when starting root-cause analysis across many failures. " +
        "Use GetFailedMessageById when inspecting a specific failed message. " +
        "Read-only."
    )]
    public async Task<string> GetFailedMessages(
        [Description("Filter failed messages by status: unresolved (still failing), resolved (succeeded on retry), archived (dismissed), or retryissued (retry in progress). Omit this filter to include all statuses.")] string? status = null,
        [Description("Restricts failed-message results to entries modified after this ISO 8601 date/time. Omitting this may return a large result set.")] string? modified = null,
        [Description("Filter failed messages to a specific queue address, for example 'Sales@machine'. Omit this filter to include all queues.")] string? queueAddress = null,
        [Description("Page number, 1-based")] int page = 1,
        [Description("Results per page")] int perPage = 50,
        [Description("Sort by: time_sent, message_type, or time_of_failure")] string sort = "time_of_failure",
        [Description("Sort direction: asc or desc")] string direction = "desc")
    {
        logger.LogInformation("MCP GetFailedMessages invoked (status={Status}, queueAddress={QueueAddress}, page={Page})", status, queueAddress, page);

        var pagingInfo = new PagingInfo(page, perPage);
        var sortInfo = new SortInfo(sort, direction);

        var results = await store.ErrorGet(status, modified, queueAddress, pagingInfo, sortInfo);

        logger.LogInformation("MCP GetFailedMessages returned {Count} results", results.QueryStats.TotalCount);

        return JsonSerializer.Serialize(new
        {
            results.QueryStats.TotalCount,
            results.Results
        }, McpJsonOptions.Default);
    }

    [McpServerTool(ReadOnly = true, Idempotent = true, Destructive = false, OpenWorld = false), Description(
        "Get detailed information about a specific failed message. " +
        "Use this when you already know the failed message ID and need to inspect its contents or failure details. " +
        "Use GetFailedMessages or GetFailureGroups to locate relevant messages before calling this tool. " +
        "Read-only."
    )]
    public async Task<string> GetFailedMessageById(
        [Description("The failed message ID from a previous failed-message query result.")] string failedMessageId)
    {
        logger.LogInformation("MCP GetFailedMessageById invoked (failedMessageId={FailedMessageId})", failedMessageId);

        var result = await store.ErrorBy(failedMessageId);

        if (result == null)
        {
            logger.LogWarning("MCP GetFailedMessageById: message '{FailedMessageId}' not found", failedMessageId);
            return JsonSerializer.Serialize(new { Error = $"Failed message '{failedMessageId}' not found." }, McpJsonOptions.Default);
        }

        return JsonSerializer.Serialize(result, McpJsonOptions.Default);
    }

    [McpServerTool(ReadOnly = true, Idempotent = true, Destructive = false, OpenWorld = false), Description(
        "Retrieve the last processing attempt for a failed message. " +
        "Use this to understand the most recent failure behavior, including exception details and processing context. " +
        "Typically used after identifying a failed message via GetFailedMessages or GetFailedMessageById. " +
        "Read-only."
    )]
    public async Task<string> GetFailedMessageLastAttempt(
        [Description("The failed message ID from a previous failed-message query result.")] string failedMessageId)
    {
        logger.LogInformation("MCP GetFailedMessageLastAttempt invoked (failedMessageId={FailedMessageId})", failedMessageId);

        var result = await store.ErrorLastBy(failedMessageId);

        if (result == null)
        {
            logger.LogWarning("MCP GetFailedMessageLastAttempt: message '{FailedMessageId}' not found", failedMessageId);
            return JsonSerializer.Serialize(new { Error = $"Failed message '{failedMessageId}' not found." }, McpJsonOptions.Default);
        }

        return JsonSerializer.Serialize(result, McpJsonOptions.Default);
    }

    [McpServerTool(ReadOnly = true, Idempotent = true, Destructive = false, OpenWorld = false), Description(
        "Read-only. Use this tool as a quick health check to see how many messages are in each failure state. " +
        "Good for questions like: 'how many errors are there?', 'what is the error situation?', or 'are there unresolved failures?'. " +
        "Returns counts for unresolved, archived, resolved, and retryissued statuses. " +
        "This is a good first tool to call when asked about the overall error situation before drilling into specific messages."
    )]
    public async Task<string> GetErrorsSummary()
    {
        logger.LogInformation("MCP GetErrorsSummary invoked");

        var result = await store.ErrorsSummary();
        return JsonSerializer.Serialize(result, McpJsonOptions.Default);
    }

    [McpServerTool(ReadOnly = true, Idempotent = true, Destructive = false, OpenWorld = false), Description(
        "Retrieve failed messages for a specific endpoint. " +
        "Use this when investigating failures in a named endpoint such as Billing or Sales. " +
        "Prefer GetFailureGroups when you need root-cause analysis across many failures. " +
        "Use GetFailedMessageLastAttempt after this when you need the most recent failure details for a specific message. " +
        "Read-only."
    )]
    public async Task<string> GetFailedMessagesByEndpoint(
        [Description("The endpoint name that owns the failed messages. Use values obtained from endpoint-aware failed-message results.")] string endpointName,
        [Description("Filter failed messages by status: unresolved, resolved, archived, or retryissued. Omit this filter to include all statuses for the endpoint.")] string? status = null,
        [Description("Restricts endpoint failed-message results to entries modified after this ISO 8601 date/time. Omitting this may return a large result set.")] string? modified = null,
        [Description("Page number, 1-based")] int page = 1,
        [Description("Results per page")] int perPage = 50,
        [Description("Sort by: time_sent, message_type, or time_of_failure")] string sort = "time_of_failure",
        [Description("Sort direction: asc or desc")] string direction = "desc")
    {
        logger.LogInformation("MCP GetFailedMessagesByEndpoint invoked (endpoint={EndpointName}, status={Status}, page={Page})", endpointName, status, page);

        var pagingInfo = new PagingInfo(page, perPage);
        var sortInfo = new SortInfo(sort, direction);

        var results = await store.ErrorsByEndpointName(status, endpointName, modified, pagingInfo, sortInfo);

        logger.LogInformation("MCP GetFailedMessagesByEndpoint returned {Count} results for endpoint '{EndpointName}'", results.QueryStats.TotalCount, endpointName);

        return JsonSerializer.Serialize(new
        {
            results.QueryStats.TotalCount,
            results.Results
        }, McpJsonOptions.Default);
    }
}
