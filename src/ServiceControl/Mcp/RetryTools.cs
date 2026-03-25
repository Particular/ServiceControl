namespace ServiceControl.Mcp;

using System.ComponentModel;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using MessageFailures;
using MessageFailures.InternalMessages;
using Microsoft.Extensions.Logging;
using ModelContextProtocol.Server;
using NServiceBus;
using Recoverability;
using Persistence;

[McpServerToolType, Description(
    "Tools for retrying failed messages.\n\n" +
    "Agent guidance:\n" +
    "1. Every tool in this group changes system state by sending failed messages back for reprocessing. Only retry after the underlying issue has been resolved.\n" +
    "2. Prefer RetryFailureGroup when all messages share the same root cause — it is the most targeted approach.\n" +
    "3. Use RetryAllFailedMessagesByEndpoint when a bug in one endpoint has been fixed.\n" +
    "4. Use RetryFailedMessagesByQueue when a queue's consumer was down and is now back.\n" +
    "5. Use RetryAllFailedMessages only as a last resort — it retries everything.\n" +
    "6. All operations are asynchronous — they return Accepted immediately and complete in the background."
)]
public class RetryTools(IMessageSession messageSession, RetryingManager retryingManager, ILogger<RetryTools> logger)
{
    [McpServerTool(ReadOnly = false, Idempotent = false, Destructive = true, OpenWorld = false), Description(
        "Use this tool to reprocess a single failed message by sending it back to its original queue. " +
        "This operation changes system state. " +
        "Good for questions like: 'retry this message', 'reprocess this failure', or 'send this message back for processing'. " +
        "The message will go through normal processing again. Only use after the underlying issue (bug fix, infrastructure problem) has been resolved. " +
        "If you need to retry many messages with the same root cause, use RetryFailureGroup instead."
    )]
    public async Task<string> RetryFailedMessage(
        [Description("The failed message ID from a previous failed-message query result.")] string failedMessageId)
    {
        logger.LogInformation("MCP RetryFailedMessage invoked (failedMessageId={FailedMessageId})", failedMessageId);

        await messageSession.SendLocal<RetryMessage>(m => m.FailedMessageId = failedMessageId);
        return JsonSerializer.Serialize(new { Status = "Accepted", Message = $"Retry requested for message '{failedMessageId}'." }, McpJsonOptions.Default);
    }

    [McpServerTool(ReadOnly = false, Idempotent = false, Destructive = true, OpenWorld = false), Description(
        "Use this tool to reprocess multiple specific failed messages at once. " +
        "This operation changes system state. " +
        "It may affect many messages. " +
        "Good for questions like: 'retry these messages', 'reprocess messages msg-1, msg-2, msg-3', or 'retry this batch'. " +
        "Prefer RetryFailureGroup when all messages share the same failure cause — use this tool when you have a specific set of message IDs to retry."
    )]
    public async Task<string> RetryFailedMessages(
        [Description("The failed message IDs from previous failed-message query results.")] string[] messageIds)
    {
        logger.LogInformation("MCP RetryFailedMessages invoked (count={Count})", messageIds.Length);

        if (messageIds.Any(string.IsNullOrEmpty))
        {
            logger.LogWarning("MCP RetryFailedMessages: rejected due to empty message IDs");
            return JsonSerializer.Serialize(new { Error = "All message IDs must be non-empty strings." }, McpJsonOptions.Default);
        }

        await messageSession.SendLocal<RetryMessagesById>(m => m.MessageUniqueIds = messageIds);
        return JsonSerializer.Serialize(new { Status = "Accepted", Message = $"Retry requested for {messageIds.Length} messages." }, McpJsonOptions.Default);
    }

    [McpServerTool(ReadOnly = false, Idempotent = false, Destructive = true, OpenWorld = false), Description(
        "Use this tool to retry all unresolved failed messages from a specific queue. " +
        "This operation changes system state. " +
        "It may affect many messages. " +
        "Good for questions like: 'retry all failures in the Sales queue', 'reprocess everything from this queue', or 'the queue consumer is back, retry its failures'. " +
        "Useful when a queue's consumer was down or misconfigured and is now fixed. Only retries messages with unresolved status."
    )]
    public async Task<string> RetryFailedMessagesByQueue(
        [Description("The full queue address including machine name, e.g. 'Sales@machine'")] string queueAddress)
    {
        logger.LogInformation("MCP RetryFailedMessagesByQueue invoked (queueAddress={QueueAddress})", queueAddress);

        await messageSession.SendLocal<RetryMessagesByQueueAddress>(m =>
        {
            m.QueueAddress = queueAddress;
            m.Status = FailedMessageStatus.Unresolved;
        });
        return JsonSerializer.Serialize(new { Status = "Accepted", Message = $"Retry requested for all failed messages in queue '{queueAddress}'." }, McpJsonOptions.Default);
    }

    [McpServerTool(ReadOnly = false, Idempotent = false, Destructive = true, OpenWorld = false), Description(
        "Use this tool to retry every unresolved failed message across all queues and endpoints. " +
        "This operation changes system state. " +
        "It may affect many messages. " +
        "Good for questions like: 'retry everything', 'reprocess all failures', or 'retry all failed messages'. " +
        "It affects all unresolved failed messages across the instance. " +
        "This is a broad operation — prefer RetryFailedMessagesByQueue, RetryAllFailedMessagesByEndpoint, or RetryFailureGroup when you can scope the retry more narrowly."
    )]
    public async Task<string> RetryAllFailedMessages()
    {
        logger.LogInformation("MCP RetryAllFailedMessages invoked");

        await messageSession.SendLocal(new RequestRetryAll());
        return JsonSerializer.Serialize(new { Status = "Accepted", Message = "Retry requested for all failed messages." }, McpJsonOptions.Default);
    }

    [McpServerTool(ReadOnly = false, Idempotent = false, Destructive = true, OpenWorld = false), Description(
        "Use this tool to retry all failed messages for a specific NServiceBus endpoint. " +
        "This operation changes system state. " +
        "It may affect many messages. " +
        "Good for questions like: 'retry all failures in the Sales endpoint', 'the bug in Shipping is fixed, retry its failures', or 'reprocess all errors for this endpoint'. " +
        "Useful when a bug in one endpoint has been fixed and all its failures should be reprocessed."
    )]
    public async Task<string> RetryAllFailedMessagesByEndpoint(
        [Description("The NServiceBus endpoint name, e.g. 'Sales' or 'Shipping.MessageHandler'")] string endpointName)
    {
        logger.LogInformation("MCP RetryAllFailedMessagesByEndpoint invoked (endpoint={EndpointName})", endpointName);

        await messageSession.SendLocal(new RequestRetryAll { Endpoint = endpointName });
        return JsonSerializer.Serialize(new { Status = "Accepted", Message = $"Retry requested for all failed messages in endpoint '{endpointName}'." }, McpJsonOptions.Default);
    }

    [McpServerTool(ReadOnly = false, Idempotent = false, Destructive = true, OpenWorld = false), Description(
        "Use this tool to retry all failed messages that share the same exception type and stack trace. " +
        "This operation changes system state. " +
        "It may affect many messages. " +
        "Good for questions like: 'retry this failure group', 'the bug causing these NullReferenceExceptions is fixed, retry them', or 'retry all messages in this group'. " +
        "This is the most targeted way to retry related failures after fixing a specific bug. " +
        "You need a failure group ID, which you can get from GetFailureGroups. " +
        "Returns InProgress if a retry is already running for this group."
    )]
    public async Task<string> RetryFailureGroup(
        [Description("The failure group ID from previous GetFailureGroups results.")] string groupId)
    {
        logger.LogInformation("MCP RetryFailureGroup invoked (groupId={GroupId})", groupId);

        if (retryingManager.IsOperationInProgressFor(groupId, RetryType.FailureGroup))
        {
            logger.LogInformation("MCP RetryFailureGroup: operation already in progress for group '{GroupId}'", groupId);
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
