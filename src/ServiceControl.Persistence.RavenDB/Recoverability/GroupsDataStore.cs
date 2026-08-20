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
        public async Task<IList<FailureGroupView>> GetUnresolvedGroupsByClassifier(string classifier, string classifierFilter, CancellationToken cancellationToken = default)
        {
            using var session = await sessionProvider.OpenSession(cancellationToken: cancellationToken);
            var query = Queryable.Where(session.Query<FailureGroupView, FailureGroupsViewIndex>(), v => v.Type == classifier);

            if (!string.IsNullOrWhiteSpace(classifierFilter))
            {
                query = query.Where(v => v.Title == classifierFilter);
            }

            var groups = await query.OrderByDescending(x => x.Last)
                .Take(200)
                .ToListAsync(cancellationToken);

            var commentIds = groups.Select(x => MakeId(x.Id)).ToArray();
            var comments = await session.Query<GroupComment, GroupCommentIndex>().Where(x => x.Id.In(commentIds))
                .ToListAsync(cancellationToken);

            foreach (var group in groups)
            {
                group.Comment = comments.FirstOrDefault(x => x.Id == MakeId(group.Id))?.Comment;
            }

            return groups;
        }

        public async Task<QueryResult<IList<FailureGroupView>>> GetArchivedGroupsByClassifier(string classifier, CancellationToken cancellationToken = default)
        {
            using var session = await sessionProvider.OpenSession(cancellationToken: cancellationToken);
            var groups = session
                .Query<FailureGroupView, ArchivedGroupsViewIndex>()
                .Statistics(out var stats)
                .Where(v => v.Type == classifier);

            var results = await groups
                .OrderByDescending(x => x.Last)
                .Take(200) // only show 200 groups
                .ToListAsync(cancellationToken);

            return new QueryResult<IList<FailureGroupView>>(results,
                stats.ToPagedQueryStatsInfo(results, group => group.Id, ("classifier", classifier)));
        }

        public async Task<QueryResult<FailureGroupView>> GetUnresolvedGroup(string groupId, string status, string modified, CancellationToken cancellationToken = default)
        {
            using var session = await sessionProvider.OpenSession(cancellationToken: cancellationToken);
            var document = await session.Advanced
                .AsyncDocumentQuery<FailureGroupView, FailureGroupsViewIndex>()
                .Statistics(out var stats)
                .WhereEquals(group => group.Id, groupId)
                .FilterByStatusWhere(status)
                .FilterByLastModifiedRange(modified)
                .FirstOrDefaultAsync(cancellationToken);

            return new QueryResult<FailureGroupView>(document, OneGroup(stats, document, groupId, status, modified));
        }

        public async Task<QueryResult<FailureGroupView>> GetArchivedGroup(string groupId, string status, string modified, CancellationToken cancellationToken = default)
        {
            using var session = await sessionProvider.OpenSession(cancellationToken: cancellationToken);
            var document = await session.Advanced
                .AsyncDocumentQuery<FailureGroupView, ArchivedGroupsViewIndex>()
                .Statistics(out var stats)
                .WhereEquals(group => group.Id, groupId)
                .FilterByStatusWhere(status)
                .FilterByLastModifiedRange(modified)
                .FirstOrDefaultAsync(cancellationToken);

            return new QueryResult<FailureGroupView>(document, OneGroup(stats, document, groupId, status, modified));
        }

        static QueryStatsInfo OneGroup(QueryStatistics stats, FailureGroupView document, string groupId, string status, string modified) =>
            stats.ToPagedQueryStatsInfo<FailureGroupView>(document is null ? [] : [document], group => group.Id,
                ("groupId", groupId), ("status", status), ("modified", modified));

        public async Task<QueryResult<IList<FailedMessageView>>> GetGroupErrors(
            string groupId,
            string status,
            string modified,
            SortInfo sortInfo,
            PagingInfo pagingInfo,
            CancellationToken cancellationToken = default
            )
        {
            using var session = await sessionProvider.OpenSession(cancellationToken: cancellationToken);
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
                .ToListAsync(cancellationToken);

            return results.ToQueryResult(stats, view => view.Id,
                ("groupId", groupId), ("status", status), ("modified", modified),
                ("page", pagingInfo.Page), ("pageSize", pagingInfo.PageSize));
        }

        public async Task<QueryStatsInfo> GetGroupErrorsCount(string groupId, string status, string modified, CancellationToken cancellationToken = default)
        {
            using var session = await sessionProvider.OpenSession(cancellationToken: cancellationToken);
            var queryResult = await session.Advanced
                .AsyncDocumentQuery<FailureGroupMessageView, FailedMessages_ByGroup>()
                .WhereEquals(view => view.FailureGroupId, groupId)
                .FilterByStatusWhere(status)
                .FilterByLastModifiedRange(modified)
                .GetQueryResultAsync(cancellationToken);

            return queryResult.ToCountQueryStatsInfo(("groupId", groupId), ("status", status), ("modified", modified));
        }

        public async Task EditComment(string groupId, string comment, CancellationToken cancellationToken = default)
        {
            using var session = await sessionProvider.OpenSession(cancellationToken: cancellationToken);
            var groupComment =
                await session.LoadAsync<GroupComment>(MakeId(groupId), cancellationToken)
                ?? new GroupComment { Id = MakeId(groupId) };

            groupComment.Comment = comment;

            await session.StoreAsync(groupComment, cancellationToken);
            await session.SaveChangesAsync(cancellationToken);
        }

        public async Task DeleteComment(string groupId, CancellationToken cancellationToken = default)
        {
            using var session = await sessionProvider.OpenSession(cancellationToken: cancellationToken);
            session.Delete(MakeId(groupId));
            await session.SaveChangesAsync(cancellationToken);
        }

        public static string MakeId(string groupId) => $"GroupComment/{groupId}";
    }
}
