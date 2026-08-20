namespace ServiceControl.Persistence
{
    using System;
    using System.Collections.Generic;
    using System.Threading;
    using System.Threading.Tasks;
    using CompositeViews.Messages;
    using Infrastructure;

    /// <summary>
    /// The single local source of message views. A persister that also holds audit data returns failed
    /// and audited messages from one query, which puts three rules on the result it returns.
    /// <list type="number">
    /// <item>Precedence. For a given <c>{ReceivingEndpoint.Name}-{MessageId}</c> the failed row must come
    /// before the audit row, because <c>ScatterGatherApiMessageView</c>
    /// deduplicates with TryAdd and would otherwise show a failed message as successfully processed.</item>
    /// <item>Paging. At most <c>PagingInfo.PageSize</c> rows after deduplication, because the scatter gather
    /// truncates and would silently drop rows if each source contributed a full page.</item>
    /// <item>Counting. A message that both failed and was audited counts once in
    /// <see cref="Infrastructure.QueryStatsInfo"/>, not once per source.</item>
    /// </list>
    /// </summary>
    public interface IMessagesViewDataStore
    {
        Task<QueryResult<IList<MessagesView>>> GetAllMessages(PagingInfo pagingInfo, SortInfo sortInfo, bool includeSystemMessages, DateTimeRange? timeSentRange = null, CancellationToken cancellationToken = default);
        Task<QueryResult<IList<MessagesView>>> GetAllMessagesForEndpoint(string endpointName, PagingInfo pagingInfo, SortInfo sortInfo, bool includeSystemMessages, DateTimeRange? timeSentRange = null, CancellationToken cancellationToken = default);
        Task<QueryResult<IList<MessagesView>>> GetAllMessagesByConversation(string conversationId, PagingInfo pagingInfo, SortInfo sortInfo, bool includeSystemMessages, CancellationToken cancellationToken = default);
        Task<QueryResult<IList<MessagesView>>> GetAllMessagesForSearch(string searchTerms, PagingInfo pagingInfo, SortInfo sortInfo, DateTimeRange? timeSentRange = null, CancellationToken cancellationToken = default);
        Task<QueryResult<IList<MessagesView>>> SearchEndpointMessages(string endpointName, string searchKeyword, PagingInfo pagingInfo, SortInfo sortInfo, DateTimeRange? timeSentRange = null, CancellationToken cancellationToken = default);
    }
}
