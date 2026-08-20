namespace ServiceControl.Persistence
{
    using System;
    using System.Collections.Generic;
    using System.Threading;
    using System.Threading.Tasks;
    using CompositeViews.Messages;
    using Infrastructure;

    public interface IMessagesViewDataStore
    {
        Task<QueryResult<IList<MessagesView>>> GetAllMessages(PagingInfo pagingInfo, SortInfo sortInfo, bool includeSystemMessages, DateTimeRange? timeSentRange = null, CancellationToken cancellationToken = default);
        Task<QueryResult<IList<MessagesView>>> GetAllMessagesForEndpoint(string endpointName, PagingInfo pagingInfo, SortInfo sortInfo, bool includeSystemMessages, DateTimeRange? timeSentRange = null, CancellationToken cancellationToken = default);
        Task<QueryResult<IList<MessagesView>>> GetAllMessagesByConversation(string conversationId, PagingInfo pagingInfo, SortInfo sortInfo, bool includeSystemMessages, CancellationToken cancellationToken = default);
        Task<QueryResult<IList<MessagesView>>> GetAllMessagesForSearch(string searchTerms, PagingInfo pagingInfo, SortInfo sortInfo, DateTimeRange? timeSentRange = null, CancellationToken cancellationToken = default);
        Task<QueryResult<IList<MessagesView>>> SearchEndpointMessages(string endpointName, string searchKeyword, PagingInfo pagingInfo, SortInfo sortInfo, DateTimeRange? timeSentRange = null, CancellationToken cancellationToken = default);
    }
}
