namespace ServiceControl.Persistence.RavenDB.Recoverability
{
    using System.Collections.Generic;
    using System.Linq;
    using System.Threading;
    using System.Threading.Tasks;
    using Raven.Client.Documents;
    using Raven.Client.Documents.Linq;
    using Raven.Client.Documents.Session;
    using ServiceControl.MessageFailures;
    using ServiceControl.MessageFailures.Api;
    using ServiceControl.Persistence.Infrastructure;
    using ServiceControl.Recoverability;

    class GroupsDataStore(IRavenSessionProvider sessionProvider) : IGroupsDataStore
    {
        public async Task<IList<FailureGroupView>> GetFailureGroupsByClassifier(string classifier, string classifierFilter)
        {
            using var session = await sessionProvider.OpenSession();
            var query = Queryable.Where(session.Query<FailureGroupView, FailureGroupsViewIndex>(), v => v.Type == classifier);

            if (!string.IsNullOrWhiteSpace(classifierFilter))
            {
                query = query.Where(v => v.Title == classifierFilter);
            }

            var groups = await query.OrderByDescending(x => x.Last)
                .Take(200)
                .ToListAsync();

            var commentIds = groups.Select(x => MakeId(x.Id)).ToArray();
            var comments = await session.Query<GroupComment, GroupCommentIndex>().Where(x => x.Id.In(commentIds))
                .ToListAsync(CancellationToken.None);

            foreach (var group in groups)
            {
                group.Comment = comments.FirstOrDefault(x => x.Id == MakeId(group.Id))?.Comment;
            }

            return groups;
        }

        public async Task<IList<FailureGroupView>> GetArchivedFailureGroupsByClassifier(string classifier)
        {
            using var session = await sessionProvider.OpenSession();
            var groups = session
                .Query<FailureGroupView, ArchivedGroupsViewIndex>()
                .Where(v => v.Type == classifier);

            var results = await groups
                .OrderByDescending(x => x.Last)
                .Take(200) // only show 200 groups
                .ToListAsync();

            return results;
        }

        public async Task<RetryBatch> GetCurrentForwardingBatch()
        {
            using var session = await sessionProvider.OpenSession();
            var nowForwarding = await session.Include<RetryBatchNowForwarding, RetryBatch>(r => r.RetryBatchId)
                .LoadAsync<RetryBatchNowForwarding>(RetryDocumentDataStore.NowForwardingDocumentId);

            return nowForwarding == null ? null : await session.LoadAsync<RetryBatch>(nowForwarding.RetryBatchId);
        }

        public async Task<QueryResult<IList<FailureGroupView>>> GetGroup(string groupId, string status, string modified)
        {
            using var session = await sessionProvider.OpenSession();
            var queryResult = await session.Advanced
                .AsyncDocumentQuery<FailureGroupView, FailureGroupsViewIndex>()
                .Statistics(out var stats)
                .WhereEquals(group => group.Id, groupId)
                .FilterByStatusWhere(status)
                .FilterByLastModifiedRange(modified)
                .ToListAsync();

            return queryResult.ToQueryResult(stats);
        }

        public async Task<QueryResult<FailureGroupView>> GetFailureGroupView(string groupId, string status, string modified)
        {
            using var session = await sessionProvider.OpenSession();
            var document = await session.Advanced
                .AsyncDocumentQuery<FailureGroupView, ArchivedGroupsViewIndex>()
                .Statistics(out var stats)
                .WhereEquals(group => group.Id, groupId)
                .FilterByStatusWhere(status)
                .FilterByLastModifiedRange(modified)
                .FirstOrDefaultAsync();

            return new QueryResult<FailureGroupView>(document, stats.ToQueryStatsInfo());
        }

        public async Task<QueryResult<IList<FailedMessageView>>> GetGroupErrors(
            string groupId,
            string status,
            string modified,
            SortInfo sortInfo,
            PagingInfo pagingInfo
            )
        {
            using var session = await sessionProvider.OpenSession();
            var query = session.Advanced
                .AsyncDocumentQuery<FailureGroupMessageView, FailedMessages_ByGroup>()
                .Statistics(out var stats)
                .WhereEquals(view => view.FailureGroupId, groupId)
                .FilterByStatusWhere(status)
                .FilterByLastModifiedRange(modified)
                .Sort(sortInfo)
                .Paging(pagingInfo)
                .SelectFields<FailedMessage>()
                .ToQueryable()
                .TransformToFailedMessageView();

            var results = await query
                .ToListAsync();

            return results.ToQueryResult(stats);
        }

        public async Task<QueryStatsInfo> GetGroupErrorsCount(string groupId, string status, string modified)
        {
            using var session = await sessionProvider.OpenSession();
            var queryResult = await session.Advanced
                .AsyncDocumentQuery<FailureGroupMessageView, FailedMessages_ByGroup>()
                .WhereEquals(view => view.FailureGroupId, groupId)
                .FilterByStatusWhere(status)
                .FilterByLastModifiedRange(modified)
                .GetQueryResultAsync();

            return queryResult.ToQueryStatsInfo();
        }

        public async Task EditComment(string groupId, string comment)
        {
            using var session = await sessionProvider.OpenSession();
            var groupComment =
                await session.LoadAsync<GroupComment>(MakeId(groupId))
                ?? new GroupComment { Id = MakeId(groupId) };

            groupComment.Comment = comment;

            await session.StoreAsync(groupComment);
            await session.SaveChangesAsync();
        }

        public async Task DeleteComment(string groupId)
        {
            using var session = await sessionProvider.OpenSession();
            session.Delete(MakeId(groupId));
            await session.SaveChangesAsync();
        }

        public static string MakeId(string groupId) => $"GroupComment/{groupId}";
    }
}
