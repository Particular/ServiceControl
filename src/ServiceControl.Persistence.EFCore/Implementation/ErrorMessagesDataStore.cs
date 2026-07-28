namespace ServiceControl.Persistence.EFCore.Implementation;

using ServiceControl.CompositeViews.Messages;
using ServiceControl.EventLog;
using ServiceControl.MessageFailures;
using ServiceControl.MessageFailures.Api;
using ServiceControl.Operations;
using ServiceControl.Persistence.Infrastructure;
using ServiceControl.Recoverability;

public class ErrorMessagesDataStore : IErrorMessageDataStore
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

    // must set StatusChangedAt + LastModified
    public Task FailedMessageMarkAsArchived(string failedMessageId) =>
        throw new NotImplementedException();

    public Task<FailedMessage[]> FailedMessagesFetch(Guid[] ids) =>
        throw new NotImplementedException();

    public Task StoreFailedErrorImport(FailedErrorImport failure) =>
        throw new NotImplementedException();

    public Task<IEditFailedMessagesManager> CreateEditFailedMessageManager() =>
        throw new NotImplementedException();

    public Task<QueryResult<FailureGroupView>> GetFailureGroupView(string groupId, string status, string modified) =>
        throw new NotImplementedException();

    public Task<IList<FailureGroupView>> GetFailureGroupsByClassifier(string classifier) =>
        throw new NotImplementedException();

    public Task<QueryResult<IList<FailedMessageView>>> ErrorGet(string status, string modified, string queueAddress, PagingInfo pagingInfo, SortInfo sortInfo) =>
        throw new NotImplementedException();

    public Task<QueryStatsInfo> ErrorsHead(string status, string modified, string queueAddress) =>
        throw new NotImplementedException();

    public Task<QueryResult<IList<FailedMessageView>>> ErrorsByEndpointName(string status, string endpointName, string modified, PagingInfo pagingInfo, SortInfo sortInfo) =>
        throw new NotImplementedException();

    public Task<IDictionary<string, object>> ErrorsSummary() =>
        throw new NotImplementedException();

    public Task<FailedMessageView> ErrorLastBy(string failedMessageId) =>
        throw new NotImplementedException();

    public Task<FailedMessage> ErrorBy(string failedMessageId) =>
        throw new NotImplementedException();

    public Task<INotificationsManager> CreateNotificationsManager() =>
        throw new NotImplementedException();

    public Task EditComment(string groupId, string comment) =>
        throw new NotImplementedException();

    public Task DeleteComment(string groupId) =>
        throw new NotImplementedException();

    public Task<QueryResult<IList<FailedMessageView>>> GetGroupErrors(string groupId, string status, string modified, SortInfo sortInfo, PagingInfo pagingInfo) =>
        throw new NotImplementedException();

    public Task<QueryStatsInfo> GetGroupErrorsCount(string groupId, string status, string modified) =>
        throw new NotImplementedException();

    public Task<QueryResult<IList<FailureGroupView>>> GetGroup(string groupId, string status, string modified) =>
        throw new NotImplementedException();

    // must set StatusChangedAt + LastModified
    public Task<bool> MarkMessageAsResolved(string failedMessageId) =>
        throw new NotImplementedException();

    public Task ProcessPendingRetries(DateTime periodFrom, DateTime periodTo, string queueAddress, Func<string, Task> processCallback) =>
        throw new NotImplementedException();

    // must set StatusChangedAt + LastModified
    public Task<string[]> UnArchiveMessagesByRange(DateTime from, DateTime to) =>
        throw new NotImplementedException();

    // must set StatusChangedAt + LastModified
    public Task<string[]> UnArchiveMessages(IEnumerable<string> failedMessageIds) =>
        throw new NotImplementedException();

    // must set StatusChangedAt + LastModified
    public Task RevertRetry(string messageUniqueId) =>
        throw new NotImplementedException();

    public Task RemoveFailedMessageRetryDocument(string uniqueMessageId) =>
        throw new NotImplementedException();

    public Task<string[]> GetRetryPendingMessages(DateTime from, DateTime to, string queueAddress) =>
        throw new NotImplementedException();

    public Task<byte[]> FetchFromFailedMessage(string uniqueMessageId) =>
        throw new NotImplementedException();

    public Task StoreEventLogItem(EventLogItem logItem) =>
        throw new NotImplementedException();

    public Task StoreFailedMessagesForTestsOnly(params FailedMessage[] failedMessages) =>
        throw new NotImplementedException();
}
