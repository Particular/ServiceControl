#nullable enable
namespace ServiceControl.Mcp;

using System.ComponentModel;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using MessageFailures.InternalMessages;
using ModelContextProtocol.Server;
using NServiceBus;
using Persistence;
using Persistence.Infrastructure;
using ServiceControl.Mcp.Authorization;

[McpServerToolType]
static class ErrorTools
{
    [McpServerTool(Name = "get_failed_messages"), Description("List failed messages. Optionally filter by status and queue address.")]
    public static async Task<string> GetFailedMessages(
        IFailedMessageQueryDataStore store,
        McpAuthorizationService authorization,
        [Description("Status filter: unresolved, archived, retryissued, or resolved.")] string? status = null,
        [Description("Filter by queue address.")] string? queueAddress = null,
        [Description("Page number (1-based).")]
        int page = 1,
        [Description("Results per page (default 50).")]
        int pageSize = 50,
        CancellationToken cancellationToken = default)
    {
        await authorization.RequirePermissionAsync(McpPermissions.ListFailures, cancellationToken);
        var results = await store.GetFailedMessages(
            status,
            null,
            queueAddress,
            new PagingInfo(page, pageSize),
            new SortInfo("time_of_failure", "desc"),
            cancellationToken);
        return JsonSerializer.Serialize(results.Results);
    }

    [McpServerTool(Name = "get_failed_message"), Description("Get a specific failed message by its ID.")]
    public static async Task<string> GetFailedMessage(
        IFailedMessageQueryDataStore store,
        McpAuthorizationService authorization,
        [Description("The unique ID of the failed message.")] string messageId,
        CancellationToken cancellationToken = default)
    {
        await authorization.RequirePermissionAsync(McpPermissions.GetFailure, cancellationToken);
        var result = await store.GetFailedMessage(messageId, cancellationToken);
        return result == null ? "Message not found." : JsonSerializer.Serialize(result);
    }

    [McpServerTool(Name = "retry_failed_message"), Description("Retry a single failed message by its ID.")]
    public static async Task<string> RetryFailedMessage(
        IMessageSession messageSession,
        McpAuthorizationService authorization,
        [Description("The unique ID of the failed message to retry.")] string messageId,
        CancellationToken cancellationToken = default)
    {
        await authorization.RequirePermissionAsync(McpPermissions.RetryFailure, cancellationToken);
        await messageSession.SendLocal<RetryMessage>(
            m => m.FailedMessageId = messageId,
            cancellationToken);
        return $"Retry requested for message {messageId}.";
    }
}