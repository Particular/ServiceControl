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
    public Task<IList<FailureGroupView>> GetUnresolvedGroupsByClassifier(string classifier, string classifierFilter) =>
        ExecuteWithDbContext(async dbContext =>
        {
            var groups = ByClassifier(dbContext, classifier);

            if (!string.IsNullOrWhiteSpace(classifierFilter))
            {
                groups = groups.Where(group => group.Title == classifierFilter);
            }

            var views = await MostRecent(groups.AggregateGroups(WithStatus(dbContext, FailedMessageStatus.Unresolved)));

            await AttachComments(dbContext, views);

            return views;
        });

    public Task<IList<FailureGroupView>> GetArchivedGroupsByClassifier(string classifier) =>
        ExecuteWithDbContext(dbContext => MostRecent(
            ByClassifier(dbContext, classifier).AggregateGroups(WithStatus(dbContext, FailedMessageStatus.Archived))));

    public Task<QueryResult<FailureGroupView>> GetUnresolvedGroup(string groupId, string status, string modified) =>
        ExecuteWithDbContext(dbContext => SingleGroup(dbContext, groupId, FailedMessageStatus.Unresolved, status, modified));

    public Task<QueryResult<FailureGroupView>> GetArchivedGroup(string groupId, string status, string modified) =>
        ExecuteWithDbContext(dbContext => SingleGroup(dbContext, groupId, FailedMessageStatus.Archived, status, modified));

    public Task<QueryResult<IList<FailedMessageView>>> GetGroupErrors(string groupId, string status, string modified, SortInfo sortInfo, PagingInfo pagingInfo) =>
        ExecuteWithDbContext(dbContext => InGroup(dbContext, groupId, status, modified).ToPagedResult(pagingInfo, sortInfo));

    public Task<QueryStatsInfo> GetGroupErrorsCount(string groupId, string status, string modified) =>
        ExecuteWithDbContext(dbContext => InGroup(dbContext, groupId, status, modified).ToQueryStatsInfo());

    public Task EditComment(string groupId, string comment) =>
        ExecuteWithDbContext(async dbContext =>
        {
            if (string.IsNullOrWhiteSpace(comment))
            {
                await RemoveComment(dbContext, groupId);
                return;
            }

            await dbContext.UpsertAsync([groupId],
                () => new GroupCommentEntity { GroupId = groupId, Comment = comment },
                entity => entity.Comment = comment);
        });

    public Task DeleteComment(string groupId) =>
        ExecuteWithDbContext(dbContext => RemoveComment(dbContext, groupId));

    static Task<int> RemoveComment(ServiceControlDbContext dbContext, string groupId) =>
        dbContext.GroupComments
            .Where(groupComment => groupComment.GroupId == groupId)
            .ExecuteDeleteAsync();

    static IQueryable<FailedMessageGroupEntity> ByClassifier(ServiceControlDbContext dbContext, string classifier) =>
        dbContext.FailedMessageGroups
            .AsNoTracking()
            .Where(group => group.Type == classifier);

    static async Task<QueryResult<FailureGroupView>> SingleGroup(ServiceControlDbContext dbContext, string groupId, FailedMessageStatus baseline, string status, string modified)
    {
        var groups = await dbContext.FailedMessageGroups
            .AsNoTracking()
            .Where(group => group.GroupId == groupId)
            .AggregateGroups(WithStatus(dbContext, baseline)
                .FilterByStatus(status)
                .FilterByLastModifiedRange(modified))
            .ToListAsync();

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

    static async Task AttachComments(ServiceControlDbContext dbContext, IList<FailureGroupView> groups)
    {
        if (groups.Count == 0)
        {
            return;
        }

        var groupIds = groups.Select(group => group.Id).ToArray();

        var comments = await dbContext.GroupComments
            .AsNoTracking()
            .Where(groupComment => groupIds.Contains(groupComment.GroupId))
            .ToDictionaryAsync(groupComment => groupComment.GroupId, groupComment => groupComment.Comment);

        foreach (var group in groups)
        {
            group.Comment = comments.GetValueOrDefault(group.Id);
        }
    }

    static async Task<IList<FailureGroupView>> MostRecent(IQueryable<FailureGroupView> groups) =>
        await groups
            .OrderByDescending(group => group.Last)
            .Take(FailureGroupQueries.MaxGroups)
            .ToListAsync();
}
