#nullable enable
namespace ServiceControl.Persistence
{
    using System;
    using System.Collections.Generic;
    using System.Threading.Tasks;
    using Infrastructure;
    using MessageFailures.Api;
    using ServiceControl.MessageFailures;

    public interface IFailedMessageQueryDataStore
    {
        Task<QueryResult<IList<FailedMessageView>>> GetFailedMessages(string? status, string? modified, string? queueAddress, PagingInfo pagingInfo, SortInfo sortInfo);
        Task<QueryStatsInfo> GetFailedMessagesStats(string? status, string? modified, string? queueAddress);
        Task<QueryResult<IList<FailedMessageView>>> GetFailedMessagesByEndpoint(string? status, string endpointName, string? modified, PagingInfo pagingInfo, SortInfo sortInfo);
        Task<IDictionary<string, object>> GetFailedMessagesSummary();
        Task<FailedMessageView?> GetLatestFailedMessageView(string failedMessageId);
        Task<FailedMessage?> GetFailedMessage(string failedMessageId);
        /// <summary>Ids with no stored failed message are skipped, so the result can be shorter than <paramref name="ids" />.</summary>
        Task<FailedMessage[]> GetFailedMessagesByIds(Guid[] ids);
    }
}
