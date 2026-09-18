#nullable enable
namespace ServiceControl.Mcp;

using System.ComponentModel;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using ModelContextProtocol.Server;
using ServiceControl.MessageFailures;
using ServiceControl.MessageFailures.Api;
using ServiceControl.Persistence;
using ServiceControl.Persistence.Infrastructure;
using ServiceControl.Mcp.Authorization;

[Description(
    "Read-only tools for investigating failed messages.\n\n" +
    "Agent guidance:\n" +
    "1. Start with GetFailedMessages to get a quick view of failures.\n" +
    "2. Use GetFailedMessagesByEndpoint when you already know the endpoint.\n" +
    "3. Use GetFailedMessageById for the full failed-message payload, or GetFailedMessageLastAttempt for the most recent failure.\n" +
    "4. Use GetErrorsSummary to understand the overall failure counts before drilling into details.\n" +
    "5. Keep page=1 unless the user asks for more results.\n" +
    "6. Only change sorting when the user explicitly asks for it.")]
public sealed class FailedMessageTools(McpAuthorizationService authorization)
{
    [McpServerTool(Name = "get_failed_messages", ReadOnly = true, Idempotent = true, Destructive = false, OpenWorld = false, UseStructuredContent = true), Description(
        "Retrieve failed messages for investigation. Use this when exploring recent failures or narrowing down failures by queue, status, or time range. " +
        "Prefer GetFailedMessagesByEndpoint when you already know the endpoint. Use GetFailedMessageById when inspecting a specific failed message. Read-only.")]
    public async Task<McpCollectionResult<FailedMessageView>> GetFailedMessages(
        IFailedMessageQueryDataStore store,
        [Description("Filter failed messages by status: unresolved, archived, retryissued, or resolved. Omit this filter to include all statuses.")] string? status = null,
        [Description("Restricts failed-message results to entries modified after this ISO 8601 date/time. Omit this filter to include all results.")] string? modified = null,
        [Description("Filter failed messages to a specific queue address, for example 'Sales@machine'. Omit this filter to include all queues.")] string? queueAddress = null,
        [Description("Page number, 1-based.")] int page = 1,
        [Description("Results per page.")] int perPage = 50,
        [Description("Sort by: time_sent, message_type, or time_of_failure.")] string sort = "time_of_failure",
        [Description("Sort direction: asc or desc.")] string direction = "desc",
        CancellationToken cancellationToken = default)
    {
        await authorization.RequirePermissionAsync(McpPermissions.ListFailures, cancellationToken);

        var results = await store.GetFailedMessages(
            McpToolInputValidation.NormalizeStatus(status),
            McpToolInputValidation.NormalizeOptionalFilter(modified),
            McpToolInputValidation.NormalizeOptionalFilter(queueAddress),
            new PagingInfo(page, perPage),
            new SortInfo(McpToolInputValidation.NormalizeSort(sort), McpToolInputValidation.NormalizeDirection(direction)),
            cancellationToken);

        return new McpCollectionResult<FailedMessageView>
        {
            TotalCount = (int)results.QueryStats.TotalCount,
            Results = results.Results?.ToArray() ?? []
        };
    }

    [McpServerTool(Name = "get_failed_message_by_id", ReadOnly = true, Idempotent = true, Destructive = false, OpenWorld = false, UseStructuredContent = true), Description(
        "Get detailed information about a specific failed message. Use this when you already know the failed message ID and need to inspect its contents or failure details. " +
        "Use GetFailedMessages or GetFailedMessagesByEndpoint to locate relevant messages before calling this tool. Read-only.")]
    public async Task<McpFailedMessageResult> GetFailedMessageById(
        IFailedMessageQueryDataStore store,
        [Description("The failed message ID from a previous failed-message query result.")] string failedMessageId,
        CancellationToken cancellationToken = default)
    {
        await authorization.RequirePermissionAsync(McpPermissions.GetFailure, cancellationToken);

        var result = await store.GetFailedMessage(failedMessageId, cancellationToken);

        return result == null
            ? new McpFailedMessageResult { Error = $"Failed message '{failedMessageId}' not found." }
            : McpFailedMessageResult.From(result);
    }

    [McpServerTool(Name = "get_failed_message_last_attempt", ReadOnly = true, Idempotent = true, Destructive = false, OpenWorld = false, UseStructuredContent = true), Description(
        "Retrieve the last processing attempt for a failed message. Use this to understand the most recent failure behavior and context. " +
        "Typically used after identifying a failed message via GetFailedMessages or GetFailedMessageById. Read-only.")]
    public async Task<McpFailedMessageViewResult> GetFailedMessageLastAttempt(
        IFailedMessageQueryDataStore store,
        [Description("The failed message ID from a previous failed-message query result.")] string failedMessageId,
        CancellationToken cancellationToken = default)
    {
        await authorization.RequirePermissionAsync(McpPermissions.GetFailureLastAttempt, cancellationToken);

        var result = await store.GetLatestFailedMessageView(failedMessageId, cancellationToken);

        return result == null
            ? new McpFailedMessageViewResult { Error = $"Failed message '{failedMessageId}' not found." }
            : McpFailedMessageViewResult.From(result);
    }

    [McpServerTool(Name = "get_errors_summary", ReadOnly = true, Idempotent = true, Destructive = false, OpenWorld = false, UseStructuredContent = true), Description(
        "Use this tool as a quick health check to see how many messages are in each failure state. Good for questions like: 'how many errors are there?' or 'are there unresolved failures?'. " +
        "Returns counts for unresolved, archived, resolved, and retryissued statuses. Read-only.")]
    public async Task<McpErrorsSummaryResult> GetErrorsSummary(
        IFailedMessageQueryDataStore store,
        CancellationToken cancellationToken = default)
    {
        await authorization.RequirePermissionAsync(McpPermissions.GetErrorsSummary, cancellationToken);

        var unresolved = store.GetFailedMessagesStats("unresolved", null, null, cancellationToken);
        var archived = store.GetFailedMessagesStats("archived", null, null, cancellationToken);
        var resolved = store.GetFailedMessagesStats("resolved", null, null, cancellationToken);
        var retryIssued = store.GetFailedMessagesStats("retryissued", null, null, cancellationToken);

        await Task.WhenAll(unresolved, archived, resolved, retryIssued);

        return McpErrorsSummaryResult.From(
            unresolved.Result.TotalCount,
            archived.Result.TotalCount,
            resolved.Result.TotalCount,
            retryIssued.Result.TotalCount);
    }

    [McpServerTool(Name = "get_failed_messages_by_endpoint", ReadOnly = true, Idempotent = true, Destructive = false, OpenWorld = false, UseStructuredContent = true), Description(
        "Retrieve failed messages for a specific endpoint. Use this when investigating failures in a named endpoint such as Billing or Sales. " +
        "Prefer GetFailedMessages when you need a broad list, and GetFailedMessageLastAttempt when you need the most recent details for a specific message. Read-only.")]
    public async Task<McpCollectionResult<FailedMessageView>> GetFailedMessagesByEndpoint(
        IFailedMessageQueryDataStore store,
        [Description("The endpoint name that owns the failed messages.")] string endpointName,
        [Description("Filter failed messages by status: unresolved, resolved, archived, or retryissued. Omit this filter to include all statuses for the endpoint.")] string? status = null,
        [Description("Restricts endpoint failed-message results to entries modified after this ISO 8601 date/time. Omit this filter to include all results.")] string? modified = null,
        [Description("Page number, 1-based.")] int page = 1,
        [Description("Results per page.")] int perPage = 50,
        [Description("Sort by: time_sent, message_type, or time_of_failure.")] string sort = "time_of_failure",
        [Description("Sort direction: asc or desc.")] string direction = "desc",
        CancellationToken cancellationToken = default)
    {
        await authorization.RequirePermissionAsync(McpPermissions.GetFailuresByEndpoint, cancellationToken);

        var results = await store.GetFailedMessagesByEndpoint(
            McpToolInputValidation.NormalizeStatus(status),
            endpointName,
            McpToolInputValidation.NormalizeOptionalFilter(modified),
            new PagingInfo(page, perPage),
            new SortInfo(McpToolInputValidation.NormalizeSort(sort), McpToolInputValidation.NormalizeDirection(direction)),
            cancellationToken);

        return new McpCollectionResult<FailedMessageView>
        {
            TotalCount = (int)results.QueryStats.TotalCount,
            Results = results.Results?.ToArray() ?? []
        };
    }
}