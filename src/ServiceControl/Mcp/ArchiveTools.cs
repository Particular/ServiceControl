namespace ServiceControl.Mcp;

using System.ComponentModel;
using System.Linq;
using System.Threading.Tasks;
using MessageFailures.InternalMessages;
using Microsoft.Extensions.Logging;
using ModelContextProtocol.Server;
using NServiceBus;
using Persistence.Recoverability;
using ServiceControl.Recoverability;
using ServiceControl.Infrastructure.Mcp;

[McpServerToolType, Description(
    "Tools for archiving and unarchiving failed messages.\n\n" +
    "Agent guidance:\n" +
    "1. Every tool in this group changes system state by archiving or restoring failed messages.\n" +
    "2. Archiving dismisses a failed message — it moves out of the unresolved list and no longer counts as an active problem.\n" +
    "3. Unarchiving restores a previously archived message back to the unresolved list so it can be retried.\n" +
    "4. Prefer ArchiveFailureGroup or UnarchiveFailureGroup when acting on an entire failure group — it is more efficient than archiving messages individually.\n" +
    "5. Use ArchiveFailedMessages or UnarchiveFailedMessages when you have a specific set of message IDs.\n" +
    "6. All operations are asynchronous — they return Accepted immediately and complete in the background."
)]
public class ArchiveTools(IMessageSession messageSession, IArchiveMessages archiver, ILogger<ArchiveTools> logger)
{
    [McpServerTool(ReadOnly = false, Idempotent = false, Destructive = true, OpenWorld = false, UseStructuredContent = true), Description(
        "Use this tool to dismiss a single failed message that does not need to be retried. " +
        "This operation changes system state. " +
        "Good for questions like: 'archive this message', 'dismiss this failure', or 'I do not need to retry this one'. " +
        "Archiving moves the message out of the unresolved list so it no longer shows up as an active problem. " +
        "This is an asynchronous operation — the message will be archived shortly after the request is accepted. " +
        "If you need to archive many messages with the same root cause, use ArchiveFailureGroup instead."
    )]
    public async Task<McpArchiveOperationResult> ArchiveFailedMessage(
        [Description("The failed message ID from a previous failed-message query result.")] string failedMessageId)
    {
        logger.LogInformation("MCP ArchiveFailedMessage invoked (failedMessageId={FailedMessageId})", failedMessageId);

        await messageSession.SendLocal<ArchiveMessage>(m => m.FailedMessageId = failedMessageId);
        return McpArchiveOperationResult.Accepted($"Archive requested for message '{failedMessageId}'.");
    }

    [McpServerTool(ReadOnly = false, Idempotent = false, Destructive = true, OpenWorld = false, UseStructuredContent = true), Description(
        "Use this tool to dismiss multiple failed messages at once that do not need to be retried. " +
        "This operation changes system state. " +
        "It may affect many messages. " +
        "Good for questions like: 'archive these messages', 'dismiss these failures', or 'archive messages msg-1, msg-2, msg-3'. " +
        "Prefer ArchiveFailureGroup when all messages share the same failure cause — use this tool when you have a specific set of message IDs to archive."
    )]
    public async Task<McpArchiveOperationResult> ArchiveFailedMessages(
        [Description("The failed message IDs from previous failed-message query results.")] string[] messageIds)
    {
        logger.LogInformation("MCP ArchiveFailedMessages invoked (count={Count})", messageIds.Length);

        if (messageIds.Any(string.IsNullOrEmpty))
        {
            logger.LogWarning("MCP ArchiveFailedMessages: rejected due to empty message IDs");
            return McpArchiveOperationResult.ValidationError("All message IDs must be non-empty strings.");
        }

        foreach (var id in messageIds)
        {
            await messageSession.SendLocal<ArchiveMessage>(m => m.FailedMessageId = id);
        }
        return McpArchiveOperationResult.Accepted($"Archive requested for {messageIds.Length} messages.");
    }

    [McpServerTool(ReadOnly = false, Idempotent = false, Destructive = true, OpenWorld = false, UseStructuredContent = true), Description(
        "Use this tool to dismiss an entire failure group — all messages that failed with the same exception type and stack trace. " +
        "This operation changes system state. " +
        "It may affect many messages. " +
        "Good for questions like: 'archive this failure group', 'dismiss all NullReferenceException failures', or 'archive the whole group'. " +
        "This is the most efficient way to archive many related failures at once. " +
        "You need a failure group ID, which you can get from GetFailureGroups. " +
        "Returns InProgress if an archive operation is already running for this group."
    )]
    public async Task<McpArchiveOperationResult> ArchiveFailureGroup(
        [Description("The failure group ID from previous GetFailureGroups results.")] string groupId)
    {
        logger.LogInformation("MCP ArchiveFailureGroup invoked (groupId={GroupId})", groupId);

        if (archiver.IsOperationInProgressFor(groupId, ArchiveType.FailureGroup))
        {
            logger.LogInformation("MCP ArchiveFailureGroup: operation already in progress for group '{GroupId}'", groupId);
            return McpArchiveOperationResult.InProgress($"An archive operation is already in progress for group '{groupId}'.");
        }

        await archiver.StartArchiving(groupId, ArchiveType.FailureGroup);
        await messageSession.SendLocal<ArchiveAllInGroup>(m => m.GroupId = groupId);

        return McpArchiveOperationResult.Accepted($"Archive requested for all messages in failure group '{groupId}'.");
    }

    [McpServerTool(ReadOnly = false, Idempotent = false, Destructive = true, OpenWorld = false, UseStructuredContent = true), Description(
        "Use this tool to restore a previously archived failed message back to the unresolved list so it can be retried. " +
        "This operation changes system state. " +
        "Good for questions like: 'unarchive this message', 'restore this failure', or 'I need to retry this archived message'. " +
        "Use when a message was archived by mistake or when the underlying issue has been fixed and the message should be reprocessed. " +
        "If you need to restore many messages from the same failure group, use UnarchiveFailureGroup instead."
    )]
    public async Task<McpArchiveOperationResult> UnarchiveFailedMessage(
        [Description("The failed message ID to restore from the archived state.")] string failedMessageId)
    {
        logger.LogInformation("MCP UnarchiveFailedMessage invoked (failedMessageId={FailedMessageId})", failedMessageId);

        await messageSession.SendLocal<UnArchiveMessages>(m => m.FailedMessageIds = [failedMessageId]);
        return McpArchiveOperationResult.Accepted($"Unarchive requested for message '{failedMessageId}'.");
    }

    [McpServerTool(ReadOnly = false, Idempotent = false, Destructive = true, OpenWorld = false, UseStructuredContent = true), Description(
        "Use this tool to restore multiple previously archived failed messages back to the unresolved list. " +
        "This operation changes system state. " +
        "It may affect many messages. " +
        "Good for questions like: 'unarchive these messages', 'restore these failures', or 'unarchive messages msg-1, msg-2, msg-3'. " +
        "Prefer UnarchiveFailureGroup when restoring an entire group — use this tool when you have a specific set of message IDs."
    )]
    public async Task<McpArchiveOperationResult> UnarchiveFailedMessages(
        [Description("The failed message IDs to restore from the archived state.")] string[] messageIds)
    {
        logger.LogInformation("MCP UnarchiveFailedMessages invoked (count={Count})", messageIds.Length);

        if (messageIds.Any(string.IsNullOrEmpty))
        {
            logger.LogWarning("MCP UnarchiveFailedMessages: rejected due to empty message IDs");
            return McpArchiveOperationResult.ValidationError("All message IDs must be non-empty strings.");
        }

        await messageSession.SendLocal<UnArchiveMessages>(m => m.FailedMessageIds = messageIds);
        return McpArchiveOperationResult.Accepted($"Unarchive requested for {messageIds.Length} messages.");
    }

    [McpServerTool(ReadOnly = false, Idempotent = false, Destructive = true, OpenWorld = false, UseStructuredContent = true), Description(
        "Use this tool to restore an entire archived failure group back to the unresolved list. " +
        "This operation changes system state. " +
        "It may affect many messages. " +
        "Good for questions like: 'unarchive this failure group', 'restore all archived NullReferenceException failures', or 'unarchive the whole group'. " +
        "All messages that were archived together under this group will become available for retry again. " +
        "You need a failure group ID, which you can get from GetFailureGroups. " +
        "Returns InProgress if an unarchive operation is already running for this group."
    )]
    public async Task<McpArchiveOperationResult> UnarchiveFailureGroup(
        [Description("The failure group ID from previous GetFailureGroups results.")] string groupId)
    {
        logger.LogInformation("MCP UnarchiveFailureGroup invoked (groupId={GroupId})", groupId);

        if (archiver.IsOperationInProgressFor(groupId, ArchiveType.FailureGroup))
        {
            logger.LogInformation("MCP UnarchiveFailureGroup: operation already in progress for group '{GroupId}'", groupId);
            return McpArchiveOperationResult.InProgress($"An unarchive operation is already in progress for group '{groupId}'.");
        }

        await archiver.StartUnarchiving(groupId, ArchiveType.FailureGroup);
        await messageSession.SendLocal<UnarchiveAllInGroup>(m => m.GroupId = groupId);

        return McpArchiveOperationResult.Accepted($"Unarchive requested for all messages in failure group '{groupId}'.");
    }
}
