namespace ServiceControl.Mcp;

using System.ComponentModel;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using MessageFailures;
using MessageFailures.InternalMessages;
using ModelContextProtocol.Server;
using NServiceBus;
using Persistence;
using Persistence.Infrastructure;
using Recoverability;

[McpServerToolType]
static class ErrorTools
{
    [McpServerTool(Name = "get_failed_messages"), Description("List failed messages. Optionally filter by status (Unresolved/Archived/RetryIssued/Resolved) and queue address.")]
    public static async Task<string> GetFailedMessages(
        IErrorMessageDataStore store,
        [Description("Status filter: Unresolved, Archived, RetryIssued, or Resolved.")] string status = null,
        [Description("Filter by queue address.")] string queueAddress = null,
        [Description("Page number (1-based).")] int page = 1,
        [Description("Results per page (default 50).")] int pageSize = 50)
    {
        var results = await store.ErrorGet(status, null, queueAddress, new PagingInfo(page, pageSize), new SortInfo("time_of_failure", "desc"));
        return JsonSerializer.Serialize(results.Results);
    }

    [McpServerTool(Name = "get_failed_messages_by_endpoint"), Description("List failed messages for a specific endpoint.")]
    public static async Task<string> GetFailedMessagesByEndpoint(
        IErrorMessageDataStore store,
        [Description("The endpoint name to filter by.")] string endpointName,
        [Description("Status filter: Unresolved, Archived, RetryIssued, or Resolved.")] string status = null,
        [Description("Page number (1-based).")] int page = 1,
        [Description("Results per page (default 50).")] int pageSize = 50)
    {
        var results = await store.ErrorsByEndpointName(status, endpointName, null, new PagingInfo(page, pageSize), new SortInfo("time_of_failure", "desc"));
        return JsonSerializer.Serialize(results.Results);
    }

    [McpServerTool(Name = "get_failed_message"), Description("Get a specific failed message by its ID.")]
    public static async Task<string> GetFailedMessage(
        IErrorMessageDataStore store,
        [Description("The unique ID of the failed message.")] string messageId)
    {
        var result = await store.ErrorBy(messageId);
        return result == null ? "Message not found." : JsonSerializer.Serialize(result);
    }

    [McpServerTool(Name = "get_errors_summary"), Description("Get a summary count of failed messages grouped by status.")]
    public static async Task<string> GetErrorsSummary(IErrorMessageDataStore store)
    {
        var result = await store.ErrorsSummary();
        return JsonSerializer.Serialize(result);
    }

    [McpServerTool(Name = "retry_failed_message"), Description("Retry a single failed message by its ID.")]
    public static async Task<string> RetryFailedMessage(
        IMessageSession messageSession,
        [Description("The unique ID of the failed message to retry.")] string messageId,
        CancellationToken cancellationToken = default)
    {
        await messageSession.SendLocal<RetryMessage>(m => m.FailedMessageId = messageId, cancellationToken);
        return $"Retry requested for message {messageId}.";
    }

    [McpServerTool(Name = "retry_failed_messages"), Description("Retry multiple failed messages by their IDs.")]
    public static async Task<string> RetryFailedMessages(
        IMessageSession messageSession,
        [Description("Array of unique message IDs to retry.")] string[] messageIds,
        CancellationToken cancellationToken = default)
    {
        await messageSession.SendLocal<RetryMessagesById>(m => m.MessageUniqueIds = messageIds, cancellationToken);
        return $"Retry requested for {messageIds.Length} messages.";
    }

    [McpServerTool(Name = "retry_failed_messages_by_queue"), Description("Retry all unresolved failed messages from a specific queue address.")]
    public static async Task<string> RetryFailedMessagesByQueue(
        IMessageSession messageSession,
        [Description("The queue address to retry messages from.")] string queueAddress,
        CancellationToken cancellationToken = default)
    {
        await messageSession.SendLocal<RetryMessagesByQueueAddress>(m =>
        {
            m.QueueAddress = queueAddress;
            m.Status = FailedMessageStatus.Unresolved;
        }, cancellationToken);
        return $"Retry requested for all unresolved messages in queue '{queueAddress}'.";
    }

    [McpServerTool(Name = "retry_all_failed_messages"), Description("Retry all unresolved failed messages. Optionally limit to a specific endpoint.")]
    public static async Task<string> RetryAllFailedMessages(
        IMessageSession messageSession,
        [Description("Limit retry to a specific endpoint name. Omit to retry all.")] string endpointName = null,
        CancellationToken cancellationToken = default)
    {
        await messageSession.SendLocal(new RequestRetryAll { Endpoint = endpointName }, cancellationToken);
        return string.IsNullOrEmpty(endpointName)
            ? "Retry all requested."
            : $"Retry all requested for endpoint '{endpointName}'.";
    }

    [McpServerTool(Name = "archive_failed_message"), Description("Archive a single failed message by its ID.")]
    public static async Task<string> ArchiveFailedMessage(
        IMessageSession messageSession,
        [Description("The unique ID of the failed message to archive.")] string messageId,
        CancellationToken cancellationToken = default)
    {
        await messageSession.SendLocal<ArchiveMessage>(m => m.FailedMessageId = messageId, cancellationToken);
        return $"Archive requested for message {messageId}.";
    }

    [McpServerTool(Name = "archive_failed_messages"), Description("Archive multiple failed messages by their IDs.")]
    public static async Task<string> ArchiveFailedMessages(
        IMessageSession messageSession,
        [Description("Array of unique message IDs to archive.")] string[] messageIds,
        CancellationToken cancellationToken = default)
    {
        foreach (var id in messageIds)
        {
            await messageSession.SendLocal<ArchiveMessage>(m => m.FailedMessageId = id, cancellationToken);
        }
        return $"Archive requested for {messageIds.Length} messages.";
    }
}
