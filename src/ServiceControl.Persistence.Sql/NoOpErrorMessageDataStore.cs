namespace ServiceControl.Persistence.Sql;

using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using ServiceControl.CompositeViews.Messages;
using ServiceControl.EventLog;
using ServiceControl.MessageFailures;
using ServiceControl.MessageFailures.Api;
using ServiceControl.Notifications;
using ServiceControl.Operations;
using ServiceControl.Persistence;
using ServiceControl.Persistence.Infrastructure;
using ServiceControl.Recoverability;

class NoOpErrorMessageDataStore : IErrorMessageDataStore
{
    static readonly QueryResult<IList<MessagesView>> EmptyMessagesViewResult =
        new([], QueryStatsInfo.Zero);

    static readonly QueryResult<IList<FailedMessageView>> EmptyFailedMessageViewResult =
        new([], QueryStatsInfo.Zero);

    static readonly QueryResult<IList<FailureGroupView>> EmptyFailureGroupViewResult =
        new([], QueryStatsInfo.Zero);

    static readonly QueryStatsInfo EmptyQueryStatsInfo = QueryStatsInfo.Zero;

    public Task<QueryResult<IList<MessagesView>>> GetAllMessages(PagingInfo pagingInfo, SortInfo sortInfo,
        bool includeSystemMessages, DateTimeRange timeSentRange = null) =>
        Task.FromResult(EmptyMessagesViewResult);

    public Task<QueryResult<IList<MessagesView>>> GetAllMessagesForEndpoint(string endpointName,
        PagingInfo pagingInfo, SortInfo sortInfo, bool includeSystemMessages, DateTimeRange timeSentRange = null) =>
        Task.FromResult(EmptyMessagesViewResult);

    public Task<QueryResult<IList<MessagesView>>> GetAllMessagesByConversation(string conversationId,
        PagingInfo pagingInfo, SortInfo sortInfo, bool includeSystemMessages) =>
        Task.FromResult(EmptyMessagesViewResult);

    public Task<QueryResult<IList<MessagesView>>> GetAllMessagesForSearch(string searchTerms, PagingInfo pagingInfo,
        SortInfo sortInfo, DateTimeRange timeSentRange = null) =>
        Task.FromResult(EmptyMessagesViewResult);

    public Task<QueryResult<IList<MessagesView>>> SearchEndpointMessages(string endpointName, string searchKeyword,
        PagingInfo pagingInfo, SortInfo sortInfo, DateTimeRange timeSentRange = null) =>
        Task.FromResult(EmptyMessagesViewResult);

    public Task FailedMessageMarkAsArchived(string failedMessageId) => Task.CompletedTask;

    public Task<FailedMessage[]> FailedMessagesFetch(Guid[] ids) => Task.FromResult(Array.Empty<FailedMessage>());

    public Task StoreFailedErrorImport(FailedErrorImport failure) => Task.CompletedTask;

    public Task<IEditFailedMessagesManager> CreateEditFailedMessageManager() =>
        Task.FromResult<IEditFailedMessagesManager>(new NoOpEditFailedMessagesManager());

    public Task<QueryResult<FailureGroupView>> GetFailureGroupView(string groupId, string status, string modified) =>
        Task.FromResult(new QueryResult<FailureGroupView>(null, EmptyQueryStatsInfo));

    public Task<IList<FailureGroupView>> GetFailureGroupsByClassifier(string classifier) =>
        Task.FromResult<IList<FailureGroupView>>([]);

    public Task<QueryResult<IList<FailedMessageView>>> ErrorGet(string status, string modified, string queueAddress,
        PagingInfo pagingInfo, SortInfo sortInfo) =>
        Task.FromResult(EmptyFailedMessageViewResult);

    public Task<QueryStatsInfo> ErrorsHead(string status, string modified, string queueAddress) =>
        Task.FromResult(EmptyQueryStatsInfo);

    public Task<QueryResult<IList<FailedMessageView>>> ErrorsByEndpointName(string status, string endpointName,
        string modified, PagingInfo pagingInfo, SortInfo sortInfo) =>
        Task.FromResult(EmptyFailedMessageViewResult);

    public Task<IDictionary<string, object>> ErrorsSummary() =>
        Task.FromResult<IDictionary<string, object>>(new Dictionary<string, object>());

    public Task<FailedMessageView> ErrorLastBy(string failedMessageId) =>
        Task.FromResult<FailedMessageView>(null);

    public Task<FailedMessage> ErrorBy(string failedMessageId) =>
        Task.FromResult<FailedMessage>(null);

    public Task<INotificationsManager> CreateNotificationsManager() =>
        Task.FromResult<INotificationsManager>(new NoOpNotificationsManager());

    public Task EditComment(string groupId, string comment) => Task.CompletedTask;

    public Task DeleteComment(string groupId) => Task.CompletedTask;

    public Task<QueryResult<IList<FailedMessageView>>> GetGroupErrors(string groupId, string status, string modified,
        SortInfo sortInfo, PagingInfo pagingInfo) =>
        Task.FromResult(EmptyFailedMessageViewResult);

    public Task<QueryStatsInfo> GetGroupErrorsCount(string groupId, string status, string modified) =>
        Task.FromResult(EmptyQueryStatsInfo);

    public Task<QueryResult<IList<FailureGroupView>>> GetGroup(string groupId, string status, string modified) =>
        Task.FromResult(EmptyFailureGroupViewResult);

    public Task<bool> MarkMessageAsResolved(string failedMessageId) => Task.FromResult(false);

    public Task ProcessPendingRetries(DateTime periodFrom, DateTime periodTo, string queueAddress,
        Func<string, Task> processCallback) => Task.CompletedTask;

    public Task<string[]> UnArchiveMessagesByRange(DateTime from, DateTime to) =>
        Task.FromResult(Array.Empty<string>());

    public Task<string[]> UnArchiveMessages(IEnumerable<string> failedMessageIds) =>
        Task.FromResult(Array.Empty<string>());

    public Task RevertRetry(string messageUniqueId) => Task.CompletedTask;

    public Task RemoveFailedMessageRetryDocument(string uniqueMessageId) => Task.CompletedTask;

    public Task<string[]> GetRetryPendingMessages(DateTime from, DateTime to, string queueAddress) =>
        Task.FromResult(Array.Empty<string>());

    public Task<byte[]> FetchFromFailedMessage(string uniqueMessageId) =>
        Task.FromResult<byte[]>(null);

    public Task StoreEventLogItem(EventLogItem logItem) => Task.CompletedTask;

    public Task StoreFailedMessagesForTestsOnly(params FailedMessage[] failedMessages) => Task.CompletedTask;

    class NoOpEditFailedMessagesManager : IEditFailedMessagesManager
    {
        public void Dispose()
        {
        }

        public Task<FailedMessage> GetFailedMessage(string failedMessageId) =>
            Task.FromResult<FailedMessage>(null);

        public Task<string> GetCurrentEditingRequestId(string failedMessageId) =>
            Task.FromResult<string>(null);

        public Task SetCurrentEditingRequestId(string editingMessageId) => Task.CompletedTask;

        public Task SetFailedMessageAsResolved() => Task.CompletedTask;

        public Task UpdateFailedMessageBody(string uniqueMessageId, byte[] newBody) => Task.CompletedTask;

        public Task SaveChanges() => Task.CompletedTask;
    }

    class NoOpNotificationsManager : INotificationsManager
    {
        public void Dispose()
        {
        }

        public Task<NotificationsSettings> LoadSettings(TimeSpan? cacheTimeout = null) =>
            Task.FromResult<NotificationsSettings>(null);

        public Task UpdateFailedMessageGroupDetails(string groupId, string title, FailedMessageStatus status) =>
            Task.CompletedTask;

        public Task SaveChanges() => Task.CompletedTask;
    }
}
