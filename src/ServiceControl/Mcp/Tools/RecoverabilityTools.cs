#nullable enable
namespace ServiceControl.Mcp;

using System.ComponentModel;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using ModelContextProtocol.Server;
using ServiceControl.MessageFailures.Api;
using ServiceControl.Mcp.Authorization;
using ServiceControl.Persistence;
using ServiceControl.Persistence.Infrastructure;
using ServiceControl.Recoverability;

[Description(
    "Read-only tools for inspecting failure groups and retry history.\n\n" +
    "Agent guidance:\n" +
    "1. GetFailureGroups is usually the best starting point for diagnosing production issues.\n" +
    "2. Call GetFailureGroups with no parameters to use the default grouping by exception type and stack trace.\n" +
    "3. Use GetRetryHistory to check whether someone has already retried a group before retrying it again.")]
public sealed class FailureGroupTools(
    GroupFetcher fetcher,
    IGroupsDataStore store,
    IRetryHistoryDataStore retryStore,
    McpAuthorizationService authorization)
{
    [McpServerTool(Name = "get_failure_groups", ReadOnly = true, Idempotent = true, Destructive = false, OpenWorld = false, UseStructuredContent = true), Description(
        "Retrieve failure groups, where failed messages are grouped by exception type and stack trace. Use this as the first step when diagnosing production issues and identifying dominant root causes. Read-only.")]
    public async Task<GroupOperation[]> GetFailureGroups(
        [Description("How to group failures. The default 'Exception Type and Stack Trace' is almost always what you want. Use 'Message Type' to group by the NServiceBus message type instead.")] string classifier = "Exception Type and Stack Trace",
        [Description("Filter failure groups by classifier text. Omit this filter to include all groups for the selected classifier.")] string? classifierFilter = null,
        CancellationToken cancellationToken = default)
    {
        await authorization.RequirePermissionAsync(McpPermissions.ListFailureGroups, cancellationToken);

        return await fetcher.GetGroups(classifier, McpToolInputValidation.NormalizeOptionalFilter(classifierFilter), cancellationToken);
    }

    [McpServerTool(Name = "get_failure_group_errors", ReadOnly = true, Idempotent = true, Destructive = false, OpenWorld = false, UseStructuredContent = true), Description(
        "List failed messages within a specific failure group. Read-only.")]
    public async Task<McpCollectionResult<FailedMessageView>> GetFailureGroupErrors(
        [Description("The failure group ID.")] string groupId,
        [Description("Status filter: Unresolved, Archived, RetryIssued, or Resolved.")] string? status = null,
        [Description("Page number, 1-based.")] int page = 1,
        [Description("Results per page.")] int perPage = 50,
        [Description("Sort by: time_sent, message_type, or time_of_failure.")] string sort = "time_of_failure",
        [Description("Sort direction: asc or desc.")] string direction = "desc",
        CancellationToken cancellationToken = default)
    {
        await authorization.RequirePermissionAsync(McpPermissions.GetFailureGroup, cancellationToken);

        var results = await store.GetGroupErrors(
            groupId,
            McpToolInputValidation.NormalizeStatus(status),
            null,
            new SortInfo(McpToolInputValidation.NormalizeSort(sort), McpToolInputValidation.NormalizeDirection(direction)),
            new PagingInfo(page, perPage),
            cancellationToken);

        return new McpCollectionResult<FailedMessageView>
        {
            TotalCount = (int)results.QueryStats.TotalCount,
            Results = results.Results?.ToArray() ?? []
        };
    }

    [McpServerTool(Name = "get_retry_history", ReadOnly = true, Idempotent = true, Destructive = false, OpenWorld = false, UseStructuredContent = true), Description(
        "Use this tool to check the history of retry operations. Good for questions like: 'has someone already retried these?' or 'what happened the last time we retried this group?'. Read-only.")]
    public async Task<RetryHistory> GetRetryHistory(
        CancellationToken cancellationToken = default)
    {
        await authorization.RequirePermissionAsync(McpPermissions.GetRetryHistory, cancellationToken);

        var retryHistory = await retryStore.GetRetryHistory(cancellationToken);
        return retryHistory.Results ?? new RetryHistory();
    }
}