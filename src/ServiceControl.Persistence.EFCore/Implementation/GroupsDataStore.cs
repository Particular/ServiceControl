namespace ServiceControl.Persistence.EFCore.Implementation;

using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using ServiceControl.MessageFailures;
using ServiceControl.MessageFailures.Api;
using ServiceControl.Persistence.EFCore.DbContexts;
using ServiceControl.Persistence.EFCore.Entities;
using ServiceControl.Persistence.EFCore.Infrastructure;
using ServiceControl.Persistence.Infrastructure;
using ServiceControl.Recoverability;

public class GroupsDataStore(IServiceScopeFactory scopeFactory) : DataStoreBase(scopeFactory), IGroupsDataStore
{
    public Task<IList<FailureGroupView>> GetUnresolvedGroupsByClassifier(string classifier, string? classifierFilter, CancellationToken cancellationToken = default) =>
        ExecuteWithDbContext<IList<FailureGroupView>>(async (dbContext, token) =>
        {
            var groups = ByClassifier(dbContext, classifier);

            if (!string.IsNullOrWhiteSpace(classifierFilter))
            {
                groups = groups.Where(group => group.Title == classifierFilter);
            }

            var views = await GetGroupViews(groups, WithStatus(dbContext, FailedMessageStatus.Unresolved), token);

            await AttachComments(dbContext, views, token);

            return views;
        }, cancellationToken);

    public Task<QueryResult<IList<FailureGroupView>>> GetArchivedGroupsByClassifier(string classifier, CancellationToken cancellationToken = default) =>
        ExecuteWithDbContext(async (dbContext, token) =>
        {
            var groups = ByClassifier(dbContext, classifier);

            var views = await GetGroupViews(groups, WithStatus(dbContext, FailedMessageStatus.Archived), token);

            return new QueryResult<IList<FailureGroupView>>(views, views.ToQueryStatsInfo("groups", views.Count));
        }, cancellationToken);

    public Task<QueryResult<FailureGroupView>> GetUnresolvedGroup(string groupId, string? status, string? modified, CancellationToken cancellationToken = default) =>
        ExecuteWithDbContext((dbContext, token) => SingleGroup(dbContext, groupId, FailedMessageStatus.Unresolved, status, modified, token), cancellationToken);

    public Task<QueryResult<FailureGroupView>> GetArchivedGroup(string groupId, string? status, string? modified, CancellationToken cancellationToken = default) =>
        ExecuteWithDbContext((dbContext, token) => SingleGroup(dbContext, groupId, FailedMessageStatus.Archived, status, modified, token), cancellationToken);

    public Task<QueryResult<IList<FailedMessageView>>> GetGroupErrors(string groupId, string? status, string? modified, SortInfo sortInfo, PagingInfo pagingInfo, CancellationToken cancellationToken = default) =>
        ExecuteWithDbContext((dbContext, token) => InGroup(dbContext, groupId, status, modified)
            .ToPagedResult(pagingInfo, sortInfo, token), cancellationToken);

    public Task<QueryStatsInfo> GetGroupErrorsCount(string groupId, string? status, string? modified, CancellationToken cancellationToken = default) =>
        ExecuteWithDbContext((dbContext, token) => InGroup(dbContext, groupId, status, modified)
            .ToCountQueryStatsInfo("failures", token), cancellationToken);

    public Task EditComment(string groupId, string comment, CancellationToken cancellationToken = default) =>
        ExecuteWithDbContext(async (dbContext, token) =>
        {
            if (string.IsNullOrWhiteSpace(comment))
            {
                await RemoveComment(dbContext, groupId, token);
                return;
            }

            await dbContext.UpsertAsync([groupId],
                () => new GroupCommentEntity { GroupId = groupId, Comment = comment },
                entity => entity.Comment = comment,
                token);
        }, cancellationToken);

    public Task DeleteComment(string groupId, CancellationToken cancellationToken = default) =>
        ExecuteWithDbContext((dbContext, token) => RemoveComment(dbContext, groupId, token), cancellationToken);

    static Task<int> RemoveComment(ServiceControlDbContext dbContext, string groupId, CancellationToken cancellationToken) =>
        dbContext.GroupComments
            .Where(groupComment => groupComment.GroupId == groupId)
            .ExecuteDeleteAsync(cancellationToken);

    static IQueryable<FailedMessageGroupEntity> ByClassifier(ServiceControlDbContext dbContext, string classifier) =>
        dbContext.FailedMessageGroups
            .AsNoTracking()
            .Where(group => group.Type == classifier);

    static async Task<QueryResult<FailureGroupView>> SingleGroup(ServiceControlDbContext dbContext, string groupId, FailedMessageStatus baseline, string? status, string? modified, CancellationToken cancellationToken)
    {
        var groups = await dbContext.FailedMessageGroups
            .AsNoTracking()
            .Where(group => group.GroupId == groupId)
            .AggregateGroups(WithStatus(dbContext, baseline)
                .FilterByStatus(status)
                .FilterByLastModifiedRange(modified))
            .ToListAsync(cancellationToken);

        return new QueryResult<FailureGroupView>(groups.FirstOrDefault()!,
            groups.ToQueryStatsInfo("groups", groups.Count));
    }

    static IQueryable<FailedMessageEntity> WithStatus(ServiceControlDbContext dbContext, FailedMessageStatus status) =>
        dbContext.FailedMessages
            .AsNoTracking()
            .Where(message => message.Status == status);

    static IQueryable<FailedMessageEntity> InGroup(ServiceControlDbContext dbContext, string groupId, string? status, string? modified) =>
        dbContext.FailedMessages
            .AsNoTracking()
            .Where(message => dbContext.FailedMessageGroups.Any(group => group.GroupId == groupId && group.FailedMessageUniqueId == message.UniqueMessageId))
            .FilterByStatus(status)
            .FilterByLastModifiedRange(modified);

    static async Task AttachComments(ServiceControlDbContext dbContext, IList<FailureGroupView> groups, CancellationToken cancellationToken)
    {
        if (groups.Count == 0)
        {
            return;
        }

        var groupIds = groups.Select(group => group.Id).ToArray();

        var comments = await dbContext.GroupComments
            .AsNoTracking()
            .Where(groupComment => groupIds.Contains(groupComment.GroupId))
            .ToDictionaryAsync(groupComment => groupComment.GroupId, groupComment => groupComment.Comment, cancellationToken);

        foreach (var group in groups)
        {
            group.Comment = comments.GetValueOrDefault(group.Id);
        }
    }

    /// <summary>
    /// Two-step group aggregate:
    /// 1. Narrow aggregate on (GroupId, Type) — avoids hashing nvarchar(max) Title per joined row.
    ///    Order by MAX(LastTimeOfFailure) desc, take MaxGroups (200), materialise.
    /// 2. Fetch Title per output group (≤200 index seeks) from the same filtered group rows.
    /// Title is functionally dependent on GroupId, so the result is identical to grouping by Title.
    /// </summary>
    static async Task<List<FailureGroupView>> GetGroupViews(IQueryable<FailedMessageGroupEntity> groups, IQueryable<FailedMessageEntity> messages, CancellationToken cancellationToken)
    {
        var summaries = await groups
            .AggregateGroupSummaries(messages)
            .OrderByDescending(summary => summary.Last)
            .Take(FailureGroupQueries.MaxGroups)
            .ToListAsync(cancellationToken);

        if (summaries.Count == 0)
        {
            return [];
        }

        var groupIds = summaries.Select(summary => summary.Id).ToArray();

        var titles = await groups
            .Where(group => groupIds.Contains(group.GroupId))
            .Select(group => new { group.GroupId, group.Title })
            .Distinct()
            .ToDictionaryAsync(group => group.GroupId, group => group.Title, cancellationToken);

        return summaries
            .Select(summary => new FailureGroupView
            {
                Id = summary.Id,
                Title = titles.GetValueOrDefault(summary.Id) ?? string.Empty,
                Type = summary.Type,
                Count = summary.Count,
                First = summary.First,
                Last = summary.Last
            })
            .ToList();
    }
}
