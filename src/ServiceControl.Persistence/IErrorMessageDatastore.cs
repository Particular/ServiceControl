namespace ServiceControl.Persistence
{
    using System;
    using System.Collections.Generic;
    using System.Threading.Tasks;
    using CompositeViews.Messages;
    using Infrastructure;
    using MessageFailures.Api;
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
        Task<IEditFailedMessagesManager> CreateEditFailedMessageManager();
        Task<QueryResult<FailureGroupView>> GetFailureGroupView(string groupId, string status, string modified);
        Task<IList<FailureGroupView>> GetFailureGroupsByClassifier(string classifier);

        // GetAllErrorsController
        Task<QueryResult<IList<FailedMessageView>>> ErrorGet(string status, string modified, string queueAddress, PagingInfo pagingInfo, SortInfo sortInfo);
        Task<QueryStatsInfo> ErrorsHead(string status, string modified, string queueAddress);
        Task<QueryResult<IList<FailedMessageView>>> ErrorsByEndpointName(string status, string endpointName, string modified, PagingInfo pagingInfo, SortInfo sortInfo);
        Task<IDictionary<string, object>> ErrorsSummary(); // TODO: Must not be object

        // GetErrorByIdController
        Task<FailedMessage> ErrorBy(Guid failedMessageId);
        Task<FailedMessageView> ErrorLastBy(Guid failedMessageId);
        Task<FailedMessage> ErrorBy(string failedMessageId);
    }
}