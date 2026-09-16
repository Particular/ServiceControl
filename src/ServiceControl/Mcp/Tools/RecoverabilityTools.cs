#nullable enable
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
using Recoverability;
using ServiceControl.Mcp.Authorization;

[McpServerToolType]
static class RecoverabilityTools
{
    [McpServerTool(Name = "get_failure_groups"), Description("List failure groups by classifier. Available classifiers: 'Exception Type and Stack Trace', 'Message Type', 'Endpoint Address', 'Endpoint Instance', 'Endpoint Name'.")]
    public static async Task<string> GetFailureGroups(
        GroupFetcher fetcher,
        McpAuthorizationService authorization,
        [Description("The classifier to group errors by. Defaults to 'Exception Type and Stack Trace'.")] string classifier = "Exception Type and Stack Trace",
        [Description("Optional filter value within the classifier.")] string? classifierFilter = null,
        CancellationToken cancellationToken = default)
    {
        await authorization.RequirePermissionAsync(McpPermissions.ListFailureGroups, cancellationToken);
        var results = await fetcher.GetGroups(classifier, classifierFilter, cancellationToken);

        return JsonSerializer.Serialize(results);
    }

    [McpServerTool(Name = "get_failure_group_errors"), Description("List failed messages within a specific failure group.")]
    public static async Task<string> GetFailureGroupErrors(
        IGroupsDataStore store,
        McpAuthorizationService authorization,
        [Description("The ID of the failure group.")] string groupId,
        [Description("Status filter: Unresolved, Archived, RetryIssued, or Resolved.")] string? status = null,
        [Description("Page number (1-based).")]
        int page = 1,
        [Description("Results per page (default 50).")]
        int pageSize = 50,
        CancellationToken cancellationToken = default)
    {
        await authorization.RequirePermissionAsync(McpPermissions.GetFailureGroup, cancellationToken);
        var results = await store.GetGroupErrors(
            groupId,
            status,
            null,
            new SortInfo(),
            new PagingInfo(page, pageSize),
            cancellationToken);

        return JsonSerializer.Serialize(results.Results);
    }

    [McpServerTool(Name = "retry_failure_group"), Description("Retry all failed messages in a failure group.")]
    public static async Task<string> RetryFailureGroup(
        IMessageSession bus,
        RetryingManager retryingManager,
        TimeProvider timeProvider,
        McpAuthorizationService authorization,
        [Description("The ID of the failure group to retry.")] string groupId,
        CancellationToken cancellationToken = default)
    {
        await authorization.RequirePermissionAsync(McpPermissions.RetryFailureGroup, cancellationToken);

        var started = timeProvider.GetUtcNow().UtcDateTime;
        if (!retryingManager.IsOperationInProgressFor(groupId, RetryType.FailureGroup))
        {
            await retryingManager.Wait(groupId, RetryType.FailureGroup, started, cancellationToken: cancellationToken);
            await bus.SendLocal(
                new RetryAllInGroup
                {
                    GroupId = groupId,
                    Started = started
                },
                cancellationToken);
        }

        return $"Retry requested for all messages in group '{groupId}'.";
    }
}