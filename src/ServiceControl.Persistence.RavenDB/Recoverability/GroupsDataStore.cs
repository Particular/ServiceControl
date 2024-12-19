namespace ServiceControl.Persistence.RavenDB.Recoverability
{
    using System.Collections.Generic;
    using System.Linq;
    using System.Threading;
    using System.Threading.Tasks;
    using Raven.Client.Documents;
    using Raven.Client.Documents.Linq;
    using ServiceControl.MessageFailures;
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

            var commentIds = groups.Select(x => GroupComment.MakeId(x.Id)).ToArray();
            var comments = await session.Query<GroupComment, GroupCommentIndex>().Where(x => x.Id.In(commentIds))
                .ToListAsync(CancellationToken.None);

            foreach (var group in groups)
            {
                group.Comment = comments.FirstOrDefault(x => x.Id == GroupComment.MakeId(group.Id))?.Comment;
            }

            return groups;
        }

        public async Task<RetryBatch> GetCurrentForwardingBatch()
        {
            using var session = await sessionProvider.OpenSession();
            var nowForwarding = await session.Include<RetryBatchNowForwarding, RetryBatch>(r => r.RetryBatchId)
                .LoadAsync<RetryBatchNowForwarding>(RetryBatchNowForwarding.Id);

            return nowForwarding == null ? null : await session.LoadAsync<RetryBatch>(nowForwarding.RetryBatchId);
        }
    }
}