namespace ServiceControl.Mcp;

using System.ComponentModel;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using MessageFailures;
using MessageFailures.InternalMessages;
using ModelContextProtocol.Server;
using NServiceBus;
using Recoverability;
using Persistence;

[McpServerToolType]
public class RetryTools(IMessageSession messageSession, RetryingManager retryingManager)
{
    [McpServerTool, Description("Retry a single failed message by its unique ID. The message will be sent back to its original queue for reprocessing.")]
    public async Task<string> RetryFailedMessage(
        [Description("The unique ID of the failed message to retry")] string failedMessageId)
    {
        await messageSession.SendLocal<RetryMessage>(m => m.FailedMessageId = failedMessageId);
        return JsonSerializer.Serialize(new { Status = "Accepted", Message = $"Retry requested for message '{failedMessageId}'." }, McpJsonOptions.Default);
    }

    [McpServerTool, Description("Retry multiple failed messages by their unique IDs. All specified messages will be sent back to their original queues for reprocessing.")]
    public async Task<string> RetryFailedMessages(
        [Description("Array of unique message IDs to retry")] string[] messageIds)
    {
        if (messageIds.Any(string.IsNullOrEmpty))
        {
            return JsonSerializer.Serialize(new { Error = "All message IDs must be non-empty strings." }, McpJsonOptions.Default);
        }

        await messageSession.SendLocal<RetryMessagesById>(m => m.MessageUniqueIds = messageIds);
        return JsonSerializer.Serialize(new { Status = "Accepted", Message = $"Retry requested for {messageIds.Length} messages." }, McpJsonOptions.Default);
    }

    [McpServerTool, Description("Retry all failed messages from a specific queue address.")]
    public async Task<string> RetryFailedMessagesByQueue(
        [Description("The queue address to retry all failed messages from")] string queueAddress)
    {
        await messageSession.SendLocal<RetryMessagesByQueueAddress>(m =>
        {
            m.QueueAddress = queueAddress;
            m.Status = FailedMessageStatus.Unresolved;
        });
        return JsonSerializer.Serialize(new { Status = "Accepted", Message = $"Retry requested for all failed messages in queue '{queueAddress}'." }, McpJsonOptions.Default);
    }

    [McpServerTool, Description("Retry all failed messages across all queues. Use with caution as this affects all unresolved failed messages.")]
    public async Task<string> RetryAllFailedMessages()
    {
        await messageSession.SendLocal(new RequestRetryAll());
        return JsonSerializer.Serialize(new { Status = "Accepted", Message = "Retry requested for all failed messages." }, McpJsonOptions.Default);
    }

    [McpServerTool, Description("Retry all failed messages for a specific endpoint.")]
    public async Task<string> RetryAllFailedMessagesByEndpoint(
        [Description("The name of the endpoint to retry all failed messages for")] string endpointName)
    {
        await messageSession.SendLocal(new RequestRetryAll { Endpoint = endpointName });
        return JsonSerializer.Serialize(new { Status = "Accepted", Message = $"Retry requested for all failed messages in endpoint '{endpointName}'." }, McpJsonOptions.Default);
    }

    [McpServerTool, Description("Retry all failed messages in a specific failure group. Failure groups are collections of messages grouped by exception type and stack trace.")]
    public async Task<string> RetryFailureGroup(
        [Description("The ID of the failure group to retry")] string groupId)
    {
        if (retryingManager.IsOperationInProgressFor(groupId, RetryType.FailureGroup))
        {
            return JsonSerializer.Serialize(new { Status = "InProgress", Message = $"A retry operation is already in progress for group '{groupId}'." }, McpJsonOptions.Default);
        }

        var started = System.DateTime.UtcNow;
        await retryingManager.Wait(groupId, RetryType.FailureGroup, started);
        await messageSession.SendLocal(new RetryAllInGroup
        {
            GroupId = groupId,
            Started = started
        });

        return JsonSerializer.Serialize(new { Status = "Accepted", Message = $"Retry requested for all messages in failure group '{groupId}'." }, McpJsonOptions.Default);
    }
}
