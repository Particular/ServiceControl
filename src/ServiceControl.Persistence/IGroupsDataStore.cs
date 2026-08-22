namespace ServiceControl.Persistence
{
    using System.Collections.Generic;
    using System.Threading;
    using System.Threading.Tasks;
    using Infrastructure;
    using MessageFailures.Api;
    using ServiceControl.Recoverability;

    public interface IGroupsDataStore
    {
        Task<IList<FailureGroupView>> GetUnresolvedGroupsByClassifier(string classifier, string? classifierFilter, CancellationToken cancellationToken = default);
        Task<QueryResult<IList<FailureGroupView>>> GetArchivedGroupsByClassifier(string classifier, CancellationToken cancellationToken = default);

        Task<QueryResult<FailureGroupView>> GetUnresolvedGroup(string groupId, string? status, string? modified, CancellationToken cancellationToken = default);
        Task<QueryResult<FailureGroupView>> GetArchivedGroup(string groupId, string? status, string? modified, CancellationToken cancellationToken = default);
        Task<QueryResult<IList<FailedMessageView>>> GetGroupErrors(string groupId, string? status, string? modified, SortInfo sortInfo, PagingInfo pagingInfo, CancellationToken cancellationToken = default);
        Task<QueryStatsInfo> GetGroupErrorsCount(string groupId, string? status, string? modified, CancellationToken cancellationToken = default);

        Task EditComment(string groupId, string comment, CancellationToken cancellationToken = default);
        Task DeleteComment(string groupId, CancellationToken cancellationToken = default);
    }
}
