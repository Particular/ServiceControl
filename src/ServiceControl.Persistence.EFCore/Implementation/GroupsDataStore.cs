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
    public Task<IList<FailureGroupView>> GetFailureGroupsByClassifier(string classifier, string classifierFilter) =>
        ExecuteWithDbContext(dbContext =>
        {
            var groups = ByClassifier(dbContext, classifier);

            if (!string.IsNullOrWhiteSpace(classifierFilter))
            {
                groups = groups.Where(group => group.Title == classifierFilter);
            }

            return MostRecent(groups.AggregateGroups(WithStatus(dbContext, FailedMessageStatus.Unresolved)));
        });

    public Task<IList<FailureGroupView>> GetArchivedFailureGroupsByClassifier(string classifier) =>
        ExecuteWithDbContext(dbContext => MostRecent(
            ByClassifier(dbContext, classifier).AggregateGroups(WithStatus(dbContext, FailedMessageStatus.Archived))));

    // Implemented once retry batches are persisted, together with IRetryDocumentDataStore.
    public Task<RetryBatch> GetCurrentForwardingBatch() =>
        throw new NotImplementedException();

    public Task<QueryResult<IList<FailureGroupView>>> GetGroup(string groupId, string status, string modified) =>
        ExecuteWithDbContext(async dbContext =>
        {
            var groups = await ById(dbContext, groupId, FailedMessageStatus.Unresolved, status, modified).ToListAsync();

            return new QueryResult<IList<FailureGroupView>>(groups, groups.ToQueryStatsInfo());
        });

    public Task<QueryResult<FailureGroupView>> GetFailureGroupView(string groupId, string status, string modified) =>
        ExecuteWithDbContext(async dbContext =>
        {
            var groups = await ById(dbContext, groupId, FailedMessageStatus.Archived, status, modified).ToListAsync();

            // A missing group is reported as a null result, the same as the RavenDB persister does.
            return new QueryResult<FailureGroupView>(groups.FirstOrDefault()!, groups.ToQueryStatsInfo());
        });

    public Task<QueryResult<IList<FailedMessageView>>> GetGroupErrors(string groupId, string status, string modified, SortInfo sortInfo, PagingInfo pagingInfo) =>
        ExecuteWithDbContext(dbContext => InGroup(dbContext, groupId, status, modified).ToPagedResult(pagingInfo, sortInfo));

    public Task<QueryStatsInfo> GetGroupErrorsCount(string groupId, string status, string modified) =>
        ExecuteWithDbContext(dbContext => InGroup(dbContext, groupId, status, modified).ToQueryStatsInfo());

    public Task EditComment(string groupId, string comment) =>
        throw new NotImplementedException();

    public Task DeleteComment(string groupId) =>
        throw new NotImplementedException();

    static IQueryable<FailedMessageGroupEntity> ByClassifier(ServiceControlDbContext dbContext, string classifier) =>
        dbContext.FailedMessageGroups
            .AsNoTracking()
            .Where(group => group.Type == classifier);

    /// <summary>
    /// The status a group is read at, before the caller's own status and modified filters narrow it
    /// further. RavenDB reads open groups out of an unresolved-only index and archived groups out of
    /// an archived-only one, which is what <paramref name="baseline" /> stands in for here.
    /// </summary>
    static IQueryable<FailureGroupView> ById(ServiceControlDbContext dbContext, string groupId, FailedMessageStatus baseline, string status, string modified) =>
        dbContext.FailedMessageGroups
            .AsNoTracking()
            .Where(group => group.GroupId == groupId)
            .AggregateGroups(WithStatus(dbContext, baseline)
                .FilterByStatus(status)
                .FilterByLastModifiedRange(modified));

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

    static async Task<IList<FailureGroupView>> MostRecent(IQueryable<FailureGroupView> groups) =>
        await groups
            .OrderByDescending(group => group.Last)
            .Take(FailureGroupQueries.MaxGroups)
            .ToListAsync();
}
