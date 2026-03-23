#nullable enable

namespace ServiceControl.Mcp;

using System.ComponentModel;
using System.Text.Json;
using System.Threading.Tasks;
using MessageFailures.Api;
using ModelContextProtocol.Server;
using Persistence;
using Persistence.Infrastructure;

[McpServerToolType, Description(
    "Tools for investigating failed messages.\n\n" +
    "Agent guidance:\n" +
    "1. Start with GetErrorsSummary to get a quick health check of failure counts by status.\n" +
    "2. Use GetFailureGroups (from FailureGroupTools) to see failures grouped by root cause before drilling into individual messages.\n" +
    "3. Use GetFailedMessages for broad listing, or GetFailedMessagesByEndpoint when you already know the endpoint.\n" +
    "4. Use GetFailedMessageById for full details including all processing attempts, or GetFailedMessageLastAttempt for just the most recent failure.\n" +
    "5. Keep page=1 unless the user asks for more results.\n" +
    "6. Only change sorting when the user explicitly asks for it."
)]
public class FailedMessageTools(IErrorMessageDataStore store)
{
    [McpServerTool, Description(
        "Use this tool to browse failed messages when the user wants to see what is failing. " +
        "Good for questions like: 'what messages are currently failing?', 'are there failures in a specific queue?', or 'what failed recently?'. " +
        "Returns a paged list of failed messages with their status, exception details, and queue information. " +
        "For broad requests, call with no parameters to get the most recent failures — only add filters when you need to narrow down results. " +
        "Prefer GetFailedMessagesByEndpoint when the user mentions a specific endpoint."
    )]
    public async Task<string> GetFailedMessages(
        [Description("Narrow results to a specific status: unresolved (still failing), resolved (succeeded on retry), archived (dismissed), or retryissued (retry in progress). Omit to include all statuses.")] string? status = null,
        [Description("Only return messages modified after this date (ISO 8601). Useful for checking recent failures.")] string? modified = null,
        [Description("Only return messages from this queue address, e.g. 'Sales@machine'. Use when investigating a specific queue.")] string? queueAddress = null,
        [Description("Page number, 1-based")] int page = 1,
        [Description("Results per page")] int perPage = 50,
        [Description("Sort by: time_sent, message_type, or time_of_failure")] string sort = "time_of_failure",
        [Description("Sort direction: asc or desc")] string direction = "desc")
    {
        var pagingInfo = new PagingInfo(page, perPage);
        var sortInfo = new SortInfo(sort, direction);

        var results = await store.ErrorGet(status, modified, queueAddress, pagingInfo, sortInfo);

        return JsonSerializer.Serialize(new
        {
            results.QueryStats.TotalCount,
            results.Results
        }, McpJsonOptions.Default);
    }

    [McpServerTool, Description(
        "Use this tool to get the full details of a specific failed message, including all processing attempts and exception information. " +
        "Good for questions like: 'show me details for this failed message', 'what exception caused this failure?', or 'how many times has this message failed?'. " +
        "You need the message's unique ID, which you can get from GetFailedMessages or GetFailureGroups results. " +
        "If you only need the most recent failure attempt, use GetFailedMessageLastAttempt instead — it returns less data."
    )]
    public async Task<string> GetFailedMessageById(
        [Description("The unique message ID from a previous query result")] string failedMessageId)
    {
        var result = await store.ErrorBy(failedMessageId);

        if (result == null)
        {
            return JsonSerializer.Serialize(new { Error = $"Failed message '{failedMessageId}' not found." }, McpJsonOptions.Default);
        }

        return JsonSerializer.Serialize(result, McpJsonOptions.Default);
    }

    [McpServerTool, Description(
        "Use this tool to see how a specific message failed most recently. " +
        "Good for questions like: 'what was the last error for this message?', 'show me the latest exception', or 'what happened on the last attempt?'. " +
        "Returns the latest processing attempt with its exception, stack trace, and headers. " +
        "Lighter than GetFailedMessageById when you only care about the most recent failure rather than the full history."
    )]
    public async Task<string> GetFailedMessageLastAttempt(
        [Description("The unique message ID from a previous query result")] string failedMessageId)
    {
        var result = await store.ErrorLastBy(failedMessageId);

        if (result == null)
        {
            return JsonSerializer.Serialize(new { Error = $"Failed message '{failedMessageId}' not found." }, McpJsonOptions.Default);
        }

        return JsonSerializer.Serialize(result, McpJsonOptions.Default);
    }

    [McpServerTool, Description(
        "Use this tool as a quick health check to see how many messages are in each failure state. " +
        "Good for questions like: 'how many errors are there?', 'what is the error situation?', or 'are there unresolved failures?'. " +
        "Returns counts for unresolved, archived, resolved, and retryissued statuses. " +
        "This is a good first tool to call when asked about the overall error situation before drilling into specific messages."
    )]
    public async Task<string> GetErrorsSummary()
    {
        var result = await store.ErrorsSummary();
        return JsonSerializer.Serialize(result, McpJsonOptions.Default);
    }

    [McpServerTool, Description(
        "Use this tool to see failed messages for a specific NServiceBus endpoint. " +
        "Good for questions like: 'what is failing in the Sales endpoint?', 'show errors for Shipping', or 'are there failures in this endpoint?'. " +
        "Returns the same paged failure data as GetFailedMessages but scoped to one endpoint. " +
        "Prefer this tool over GetFailedMessages when the user mentions a specific endpoint name."
    )]
    public async Task<string> GetFailedMessagesByEndpoint(
        [Description("The NServiceBus endpoint name, e.g. 'Sales' or 'Shipping.MessageHandler'")] string endpointName,
        [Description("Narrow results to a specific status: unresolved, resolved, archived, or retryissued. Omit to include all.")] string? status = null,
        [Description("Only return messages modified after this date (ISO 8601)")] string? modified = null,
        [Description("Page number, 1-based")] int page = 1,
        [Description("Results per page")] int perPage = 50,
        [Description("Sort by: time_sent, message_type, or time_of_failure")] string sort = "time_of_failure",
        [Description("Sort direction: asc or desc")] string direction = "desc")
    {
        var pagingInfo = new PagingInfo(page, perPage);
        var sortInfo = new SortInfo(sort, direction);

        var results = await store.ErrorsByEndpointName(status, endpointName, modified, pagingInfo, sortInfo);

        return JsonSerializer.Serialize(new
        {
            results.QueryStats.TotalCount,
            results.Results
        }, McpJsonOptions.Default);
    }
}
