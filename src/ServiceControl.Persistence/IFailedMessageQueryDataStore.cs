#nullable enable
namespace ServiceControl.Persistence
{
    using System;
    using System.Collections.Generic;
    using System.Threading;
    using System.Threading.Tasks;
    using Infrastructure;
    using MessageFailures.Api;
    using ServiceControl.MessageFailures;

    public interface IFailedMessageQueryDataStore
    {
        Task<QueryResult<IList<FailedMessageView>>> GetFailedMessages(string? status, string? modified, string? queueAddress, PagingInfo pagingInfo, SortInfo sortInfo, CancellationToken cancellationToken = default);
        Task<QueryStatsInfo> GetFailedMessagesStats(string? status, string? modified, string? queueAddress, CancellationToken cancellationToken = default);
        Task<QueryResult<IList<FailedMessageView>>> GetFailedMessagesByEndpoint(string? status, string endpointName, string? modified, PagingInfo pagingInfo, SortInfo sortInfo, CancellationToken cancellationToken = default);
        Task<IDictionary<string, object>> GetFailedMessagesSummary(CancellationToken cancellationToken = default);
        Task<FailedMessageView?> GetLatestFailedMessageView(string failedMessageId, CancellationToken cancellationToken = default);
        Task<FailedMessage?> GetFailedMessage(string failedMessageId, CancellationToken cancellationToken = default);
        /// <summary>Ids with no stored failed message are skipped, so the result can be shorter than <paramref name="ids" />.</summary>
        Task<FailedMessage[]> GetFailedMessagesByIds(Guid[] ids, CancellationToken cancellationToken = default);
    }
}
