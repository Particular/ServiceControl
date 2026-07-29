namespace ServiceControl.Persistence.EFCore.Implementation;

using ServiceControl.CompositeViews.Messages;
using ServiceControl.Persistence.Infrastructure;

public class MessagesViewDataStore : IMessagesViewDataStore
{
    public Task<QueryResult<IList<MessagesView>>> GetAllMessages(PagingInfo pagingInfo, SortInfo sortInfo, bool includeSystemMessages, DateTimeRange? timeSentRange = null) =>
        throw new NotImplementedException();

    public Task<QueryResult<IList<MessagesView>>> GetAllMessagesForEndpoint(string endpointName, PagingInfo pagingInfo, SortInfo sortInfo, bool includeSystemMessages, DateTimeRange? timeSentRange = null) =>
        throw new NotImplementedException();

    public Task<QueryResult<IList<MessagesView>>> GetAllMessagesByConversation(string conversationId, PagingInfo pagingInfo, SortInfo sortInfo, bool includeSystemMessages) =>
        throw new NotImplementedException();

    public Task<QueryResult<IList<MessagesView>>> GetAllMessagesForSearch(string searchTerms, PagingInfo pagingInfo, SortInfo sortInfo, DateTimeRange? timeSentRange = null) =>
        throw new NotImplementedException();

    public Task<QueryResult<IList<MessagesView>>> SearchEndpointMessages(string endpointName, string searchKeyword, PagingInfo pagingInfo, SortInfo sortInfo, DateTimeRange? timeSentRange = null) =>
        throw new NotImplementedException();
}
