namespace ServiceControl.Persistence
{
    using System.Collections.Generic;
    using System.Threading.Tasks;
    using Infrastructure;
    using MessageFailures.Api;
    using ServiceControl.Recoverability;

    public interface IGroupsDataStore
    {
        Task<IList<FailureGroupView>> GetFailureGroupsByClassifier(string classifier, string classifierFilter);
        Task<IList<FailureGroupView>> GetArchivedFailureGroupsByClassifier(string classifier);
        Task<RetryBatch> GetCurrentForwardingBatch();

        Task<QueryResult<IList<FailureGroupView>>> GetGroup(string groupId, string status, string modified);
        Task<QueryResult<FailureGroupView>> GetFailureGroupView(string groupId, string status, string modified);
        Task<QueryResult<IList<FailedMessageView>>> GetGroupErrors(string groupId, string status, string modified, SortInfo sortInfo, PagingInfo pagingInfo);
        Task<QueryStatsInfo> GetGroupErrorsCount(string groupId, string status, string modified);

        Task EditComment(string groupId, string comment);
        Task DeleteComment(string groupId);
    }
}
