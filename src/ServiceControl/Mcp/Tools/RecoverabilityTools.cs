namespace ServiceControl.Mcp;

using System;
using System.ComponentModel;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using ModelContextProtocol.Server;
using NServiceBus;
using Persistence;
using Persistence.Infrastructure;
using Persistence.Recoverability;
using Recoverability;
using Recoverability.API;

[McpServerToolType]
static class RecoverabilityTools
{
    [McpServerTool(Name = "get_error_groups"), Description("List error groups by classifier. Available classifiers: 'Exception Type and Stack Trace', 'Message Type', 'Endpoint Address', 'Endpoint Instance', 'Endpoint Name'.")]
    public static async Task<string> GetErrorGroups(
        GroupFetcher fetcher,
        [Description("The classifier to group errors by. Defaults to 'Exception Type and Stack Trace'.")] string classifier = "Exception Type and Stack Trace",
        [Description("Optional filter value within the classifier.")] string classifierFilter = null)
    {
        var results = await fetcher.GetGroups(classifier, classifierFilter);
        return JsonSerializer.Serialize(results);
    }

    [McpServerTool(Name = "get_error_group_errors"), Description("List failed messages within a specific error group.")]
    public static async Task<string> GetErrorGroupErrors(
        IErrorMessageDataStore store,
        [Description("The ID of the error group.")] string groupId,
        [Description("Status filter: Unresolved, Archived, RetryIssued, or Resolved.")] string status = null,
        [Description("Page number (1-based).")] int page = 1,
        [Description("Results per page (default 50).")] int pageSize = 50)
    {
        var results = await store.GetGroupErrors(groupId, status, null, new SortInfo(), new PagingInfo(page, pageSize));
        return JsonSerializer.Serialize(results.Results);
    }

    [McpServerTool(Name = "retry_error_group"), Description("Retry all failed messages in an error group.")]
    public static async Task<string> RetryErrorGroup(
        IMessageSession bus,
        RetryingManager retryingManager,
        [Description("The ID of the error group to retry.")] string groupId,
        CancellationToken cancellationToken = default)
    {
        var started = DateTime.UtcNow;
        if (!retryingManager.IsOperationInProgressFor(groupId, RetryType.FailureGroup))
        {
            await retryingManager.Wait(groupId, RetryType.FailureGroup, started);
            await bus.SendLocal(new RetryAllInGroup { GroupId = groupId, Started = started }, cancellationToken);
        }
        return $"Retry requested for all messages in group '{groupId}'.";
    }

    [McpServerTool(Name = "archive_error_group"), Description("Archive all failed messages in an error group.")]
    public static async Task<string> ArchiveErrorGroup(
        IMessageSession bus,
        IArchiveMessages archiver,
        [Description("The ID of the error group to archive.")] string groupId,
        CancellationToken cancellationToken = default)
    {
        if (!archiver.IsOperationInProgressFor(groupId, ArchiveType.FailureGroup))
        {
            await archiver.StartArchiving(groupId, ArchiveType.FailureGroup);
            await bus.SendLocal<ArchiveAllInGroup>(m => m.GroupId = groupId, cancellationToken);
        }
        return $"Archive requested for all messages in group '{groupId}'.";
    }

    [McpServerTool(Name = "get_retry_history"), Description("Get the history of retry batch operations.")]
    public static async Task<string> GetRetryHistory(IRetryHistoryDataStore store)
    {
        var result = await store.GetRetryHistory();
        return JsonSerializer.Serialize(result);
    }
}
