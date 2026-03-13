namespace ServiceControl.Mcp;

using System.ComponentModel;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using MessageFailures.InternalMessages;
using ModelContextProtocol.Server;
using NServiceBus;
using Persistence.Recoverability;
using ServiceControl.Recoverability;

[McpServerToolType]
public class ArchiveTools(IMessageSession messageSession, IArchiveMessages archiver)
{
    [McpServerTool, Description("Archive a single failed message by its unique ID. The message will be moved to the archived status.")]
    public async Task<string> ArchiveFailedMessage(
        [Description("The unique ID of the failed message to archive")] string failedMessageId)
    {
        await messageSession.SendLocal<ArchiveMessage>(m => m.FailedMessageId = failedMessageId);
        return JsonSerializer.Serialize(new { Status = "Accepted", Message = $"Archive requested for message '{failedMessageId}'." }, McpJsonOptions.Default);
    }

    [McpServerTool, Description("Archive multiple failed messages by their unique IDs. All specified messages will be moved to the archived status.")]
    public async Task<string> ArchiveFailedMessages(
        [Description("Array of unique message IDs to archive")] string[] messageIds)
    {
        if (messageIds.Any(string.IsNullOrEmpty))
        {
            return JsonSerializer.Serialize(new { Error = "All message IDs must be non-empty strings." }, McpJsonOptions.Default);
        }

        foreach (var id in messageIds)
        {
            await messageSession.SendLocal<ArchiveMessage>(m => m.FailedMessageId = id);
        }
        return JsonSerializer.Serialize(new { Status = "Accepted", Message = $"Archive requested for {messageIds.Length} messages." }, McpJsonOptions.Default);
    }

    [McpServerTool, Description("Archive all failed messages in a specific failure group. Failure groups are collections of messages grouped by exception type and stack trace.")]
    public async Task<string> ArchiveFailureGroup(
        [Description("The ID of the failure group to archive")] string groupId)
    {
        if (archiver.IsOperationInProgressFor(groupId, ArchiveType.FailureGroup))
        {
            return JsonSerializer.Serialize(new { Status = "InProgress", Message = $"An archive operation is already in progress for group '{groupId}'." }, McpJsonOptions.Default);
        }

        await archiver.StartArchiving(groupId, ArchiveType.FailureGroup);
        await messageSession.SendLocal<ArchiveAllInGroup>(m => m.GroupId = groupId);

        return JsonSerializer.Serialize(new { Status = "Accepted", Message = $"Archive requested for all messages in failure group '{groupId}'." }, McpJsonOptions.Default);
    }

    [McpServerTool, Description("Unarchive a single failed message by its unique ID. The message will be moved back to the unresolved status.")]
    public async Task<string> UnarchiveFailedMessage(
        [Description("The unique ID of the failed message to unarchive")] string failedMessageId)
    {
        await messageSession.SendLocal<UnArchiveMessages>(m => m.FailedMessageIds = [failedMessageId]);
        return JsonSerializer.Serialize(new { Status = "Accepted", Message = $"Unarchive requested for message '{failedMessageId}'." }, McpJsonOptions.Default);
    }

    [McpServerTool, Description("Unarchive multiple failed messages by their unique IDs. All specified messages will be moved back to the unresolved status.")]
    public async Task<string> UnarchiveFailedMessages(
        [Description("Array of unique message IDs to unarchive")] string[] messageIds)
    {
        if (messageIds.Any(string.IsNullOrEmpty))
        {
            return JsonSerializer.Serialize(new { Error = "All message IDs must be non-empty strings." }, McpJsonOptions.Default);
        }

        await messageSession.SendLocal<UnArchiveMessages>(m => m.FailedMessageIds = messageIds);
        return JsonSerializer.Serialize(new { Status = "Accepted", Message = $"Unarchive requested for {messageIds.Length} messages." }, McpJsonOptions.Default);
    }

    [McpServerTool, Description("Unarchive all failed messages in a specific failure group. Failure groups are collections of messages grouped by exception type and stack trace.")]
    public async Task<string> UnarchiveFailureGroup(
        [Description("The ID of the failure group to unarchive")] string groupId)
    {
        if (archiver.IsOperationInProgressFor(groupId, ArchiveType.FailureGroup))
        {
            return JsonSerializer.Serialize(new { Status = "InProgress", Message = $"An archive operation is already in progress for group '{groupId}'." }, McpJsonOptions.Default);
        }

        await archiver.StartUnarchiving(groupId, ArchiveType.FailureGroup);
        await messageSession.SendLocal<UnarchiveAllInGroup>(m => m.GroupId = groupId);

        return JsonSerializer.Serialize(new { Status = "Accepted", Message = $"Unarchive requested for all messages in failure group '{groupId}'." }, McpJsonOptions.Default);
    }
}
