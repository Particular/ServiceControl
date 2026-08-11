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
    public Task<IList<FailureGroupView>> GetUnresolvedGroupsByClassifier(string classifier, string classifierFilter, CancellationToken cancellationToken = default) =>
        ExecuteWithDbContext(async dbContext =>
        {
            var groups = ByClassifier(dbContext, classifier);

            if (!string.IsNullOrWhiteSpace(classifierFilter))
            {
                groups = groups.Where(group => group.Title == classifierFilter);
            }

            var views = await MostRecent(groups.AggregateGroups(WithStatus(dbContext, FailedMessageStatus.Unresolved)), cancellationToken);

            await AttachComments(dbContext, views, cancellationToken);

            return views;
        });

    public Task<IList<FailureGroupView>> GetArchivedGroupsByClassifier(string classifier, CancellationToken cancellationToken = default) =>
        ExecuteWithDbContext(dbContext => MostRecent(
            ByClassifier(dbContext, classifier).AggregateGroups(WithStatus(dbContext, FailedMessageStatus.Archived)), cancellationToken));

    public Task<QueryResult<FailureGroupView>> GetUnresolvedGroup(string groupId, string status, string modified, CancellationToken cancellationToken = default) =>
        ExecuteWithDbContext(dbContext => SingleGroup(dbContext, groupId, FailedMessageStatus.Unresolved, status, modified, cancellationToken));

    public Task<QueryResult<FailureGroupView>> GetArchivedGroup(string groupId, string status, string modified, CancellationToken cancellationToken = default) =>
        ExecuteWithDbContext(dbContext => SingleGroup(dbContext, groupId, FailedMessageStatus.Archived, status, modified, cancellationToken));

    public Task<QueryResult<IList<FailedMessageView>>> GetGroupErrors(string groupId, string status, string modified, SortInfo sortInfo, PagingInfo pagingInfo, CancellationToken cancellationToken = default) =>
        ExecuteWithDbContext(dbContext => InGroup(dbContext, groupId, status, modified).ToPagedResult(pagingInfo, sortInfo, cancellationToken));

    public Task<QueryStatsInfo> GetGroupErrorsCount(string groupId, string status, string modified, CancellationToken cancellationToken = default) =>
        ExecuteWithDbContext(dbContext => InGroup(dbContext, groupId, status, modified).ToQueryStatsInfo(cancellationToken));

    public Task EditComment(string groupId, string comment, CancellationToken cancellationToken = default) =>
        ExecuteWithDbContext(async dbContext =>
        {
            if (string.IsNullOrWhiteSpace(comment))
            {
                await RemoveComment(dbContext, groupId, cancellationToken);
                return;
            }

            await dbContext.UpsertAsync([groupId],
                () => new GroupCommentEntity { GroupId = groupId, Comment = comment },
                entity => entity.Comment = comment,
                cancellationToken);
        });

    public Task DeleteComment(string groupId, CancellationToken cancellationToken = default) =>
        ExecuteWithDbContext(dbContext => RemoveComment(dbContext, groupId, cancellationToken));

    static Task<int> RemoveComment(ServiceControlDbContext dbContext, string groupId, CancellationToken cancellationToken) =>
        dbContext.GroupComments
            .Where(groupComment => groupComment.GroupId == groupId)
            .ExecuteDeleteAsync(cancellationToken);

    static IQueryable<FailedMessageGroupEntity> ByClassifier(ServiceControlDbContext dbContext, string classifier) =>
        dbContext.FailedMessageGroups
            .AsNoTracking()
            .Where(group => group.Type == classifier);

    static async Task<QueryResult<FailureGroupView>> SingleGroup(ServiceControlDbContext dbContext, string groupId, FailedMessageStatus baseline, string status, string modified, CancellationToken cancellationToken)
    {
        var groups = await dbContext.FailedMessageGroups
            .AsNoTracking()
            .Where(group => group.GroupId == groupId)
            .AggregateGroups(WithStatus(dbContext, baseline)
                .FilterByStatus(status)
                .FilterByLastModifiedRange(modified))
            .ToListAsync(cancellationToken);

        return new QueryResult<FailureGroupView>(groups.FirstOrDefault()!, groups.ToQueryStatsInfo());
    }

    static IQueryable<FailedMessageEntity> WithStatus(ServiceControlDbContext dbContext, FailedMessageStatus status) =>
        dbContext.FailedMessages
            .AsNoTracking()
            .Where(message => message.Status == status);

    static IQueryable<FailedMessageEntity> InGroup(ServiceControlDbContext dbContext, string groupId, string status, string modified) =>
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

    static async Task<IList<FailureGroupView>> MostRecent(IQueryable<FailureGroupView> groups, CancellationToken cancellationToken) =>
        await groups
            .OrderByDescending(group => group.Last)
            .Take(FailureGroupQueries.MaxGroups)
            .ToListAsync(cancellationToken);
}
