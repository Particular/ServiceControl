namespace ServiceControl.Persistence
{
    using System;
    using System.Collections.Generic;
    using System.Threading.Tasks;
    using CompositeViews.Messages;
    using Infrastructure;
    using ServiceControl.MessageFailures;
    using ServiceControl.Operations;
    using ServiceControl.Recoverability;

    public interface IErrorMessageDataStore
    {
        Task<QueryResult<IList<MessagesView>>> GetAllMessages(PagingInfo pagingInfo, SortInfo sortInfo, bool includeSystemMessages);
        Task<QueryResult<IList<MessagesView>>> GetAllMessagesForEndpoint(string endpointName, PagingInfo pagingInfo, SortInfo sortInfo, bool includeSystemMessages);
        Task<QueryResult<IList<MessagesView>>> GetAllMessagesByConversation(string conversationId, PagingInfo pagingInfo, SortInfo sortInfo, bool includeSystemMessages);
        Task<QueryResult<IList<MessagesView>>> GetAllMessagesForSearch(string searchTerms, PagingInfo pagingInfo, SortInfo sortInfo);
        Task<QueryResult<IList<MessagesView>>> GetAllMessagesForEndpoint(string searchTerms, string receivingEndpointName, PagingInfo pagingInfo, SortInfo sortInfo);
        Task<FailedMessage> FailedMessageFetch(string failedMessageId);
        Task FailedMessageMarkAsArchived(string failedMessageId);
        Task<FailedMessage[]> FailedMessagesFetch(Guid[] ids);
        Task StoreFailedErrorImport(FailedErrorImport failure);
        Task<AbstractEditFailedMessagesManager> CreateEditFailedMessageManager(string failedMessageId);
        Task<QueryResult<FailureGroupView>> GetFailureGroupView(string groupId, string status, string modified);
        Task<IList<FailureGroupView>> GetFailureGroupsByClassifier(string classifier);
    }
}