namespace ServiceControl.Persistence.EFCore.Implementation;

using ServiceControl.MessageFailures.Api;
using ServiceControl.Persistence.Infrastructure;
using ServiceControl.Recoverability;

public class GroupsDataStore : IGroupsDataStore
{
    public Task<IList<FailureGroupView>> GetFailureGroupsByClassifier(string classifier, string classifierFilter) =>
        throw new NotImplementedException();

    public Task<IList<FailureGroupView>> GetArchivedFailureGroupsByClassifier(string classifier) =>
        throw new NotImplementedException();

    public Task<RetryBatch> GetCurrentForwardingBatch() =>
        throw new NotImplementedException();

    public Task<QueryResult<IList<FailureGroupView>>> GetGroup(string groupId, string status, string modified) =>
        throw new NotImplementedException();

    public Task<QueryResult<FailureGroupView>> GetFailureGroupView(string groupId, string status, string modified) =>
        throw new NotImplementedException();

    public Task<QueryResult<IList<FailedMessageView>>> GetGroupErrors(string groupId, string status, string modified, SortInfo sortInfo, PagingInfo pagingInfo) =>
        throw new NotImplementedException();

    public Task<QueryStatsInfo> GetGroupErrorsCount(string groupId, string status, string modified) =>
        throw new NotImplementedException();

    public Task EditComment(string groupId, string comment) =>
        throw new NotImplementedException();

    public Task DeleteComment(string groupId) =>
        throw new NotImplementedException();
}
