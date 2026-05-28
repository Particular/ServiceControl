#nullable enable
namespace ServiceControl.Persistence
{
    using System;
    using System.Collections.Generic;
    using System.Threading.Tasks;
    using CompositeViews.Messages;
    using Infrastructure;
    using MessageFailures.Api;
    using ServiceControl.EventLog;
    using ServiceControl.Infrastructure.Auth.Rbac;
    using ServiceControl.MessageFailures;
    using ServiceControl.Operations;
    using ServiceControl.Recoverability;

    public interface IErrorMessageDataStore
    {
        Task<QueryResult<IList<MessagesView>>> GetAllMessages(PagingInfo pagingInfo, SortInfo sortInfo, bool includeSystemMessages, DateTimeRange timeSentRange = null);
        Task<QueryResult<IList<MessagesView>>> GetAllMessagesForEndpoint(string endpointName, PagingInfo pagingInfo, SortInfo sortInfo, bool includeSystemMessages, DateTimeRange timeSentRange = null);
        Task<QueryResult<IList<MessagesView>>> GetAllMessagesByConversation(string conversationId, PagingInfo pagingInfo, SortInfo sortInfo, bool includeSystemMessages);
        Task<QueryResult<IList<MessagesView>>> GetAllMessagesForSearch(string searchTerms, PagingInfo pagingInfo, SortInfo sortInfo, DateTimeRange timeSentRange = null);
        Task<QueryResult<IList<MessagesView>>> SearchEndpointMessages(string endpointName, string searchKeyword, PagingInfo pagingInfo, SortInfo sortInfo, DateTimeRange timeSentRange = null);
        Task FailedMessageMarkAsArchived(string failedMessageId);
        Task<FailedMessage[]> FailedMessagesFetch(Guid[] ids);
        Task StoreFailedErrorImport(FailedErrorImport failure);
        Task<IEditFailedMessagesManager> CreateEditFailedMessageManager();
        Task<QueryResult<FailureGroupView>> GetFailureGroupView(string groupId, string status, string modified);
        Task<IList<FailureGroupView>> GetFailureGroupsByClassifier(string classifier);

        // GetAllErrorsController
        /// <summary>
        /// Returns a paged list of failed messages, optionally filtered to the caller's permitted queue scope.
        /// <para>
        /// <paramref name="queueScope"/> is resolved by the controller from the caller's effective RBAC grants
        /// (via <see cref="IPermissionEvaluator.ResolveQueueScope"/>) and pushed into the query <em>before</em>
        /// paging so that the <c>Total-Count</c> header reflects only messages the caller is allowed to see.
        /// Pass <see langword="null"/> for unrestricted (admin) access.
        /// </para>
        /// </summary>
        Task<QueryResult<IList<FailedMessageView>>> ErrorGet(string status, string modified, string queueAddress, PagingInfo pagingInfo, SortInfo sortInfo, ResourceScope? queueScope = null);
        Task<QueryStatsInfo> ErrorsHead(string status, string modified, string queueAddress);
        /// <summary>
        /// Returns a paged list of failed messages for the specified endpoint, optionally filtered to
        /// the caller's permitted queue scope — same semantics as <see cref="ErrorGet"/>.
        /// </summary>
        Task<QueryResult<IList<FailedMessageView>>> ErrorsByEndpointName(string status, string endpointName, string modified, PagingInfo pagingInfo, SortInfo sortInfo, ResourceScope? queueScope = null);
        Task<IDictionary<string, object>> ErrorsSummary();

        // GetErrorByIdController
        Task<FailedMessageView> ErrorLastBy(string failedMessageId);

        //EditFailedMessagesController
        // GetErrorByIdController
        Task<FailedMessage> ErrorBy(string failedMessageId);

        //NotificationsController
        Task<INotificationsManager> CreateNotificationsManager();

        // FailureGroupsController
        Task EditComment(string groupId, string comment);
        Task DeleteComment(string groupId);
        /// <summary>
        /// Returns a paged list of failed messages for the specified failure group, optionally filtered
        /// to the caller's permitted queue scope — same semantics as <see cref="ErrorGet"/>.
        /// </summary>
        Task<QueryResult<IList<FailedMessageView>>> GetGroupErrors(string groupId, string status, string modified, SortInfo sortInfo, PagingInfo pagingInfo, ResourceScope? queueScope = null);
        Task<QueryStatsInfo> GetGroupErrorsCount(string groupId, string status, string modified);

        Task<QueryResult<IList<FailureGroupView>>> GetGroup(string groupId, string status, string modified);

        // LegacyMessageFailureResolvedHandler
        Task<bool> MarkMessageAsResolved(string failedMessageId);

        // MessageFailureResolvedHandler
        Task ProcessPendingRetries(DateTime periodFrom, DateTime periodTo, string queueAddress, Func<string, Task> processCallback);

        // UnArchiveMessagesByRangeHandler
        Task<string[]> UnArchiveMessagesByRange(DateTime from, DateTime to);

        // UnArchiveMessagesHandler
        Task<string[]> UnArchiveMessages(IEnumerable<string> failedMessageIds);

        // ReturnToSenderDequeuer.CaptureIfMessageSendingFails
        Task RevertRetry(string messageUniqueId);
        Task RemoveFailedMessageRetryDocument(string uniqueMessageId);
        Task<string[]> GetRetryPendingMessages(DateTime from, DateTime to, string queueAddress);

        // ReturnToSender.FetchFromFailedMessage
        Task<byte[]> FetchFromFailedMessage(string uniqueMessageId);

        // AuditEventLogWriter
        Task StoreEventLogItem(EventLogItem logItem);

        Task StoreFailedMessagesForTestsOnly(params FailedMessage[] failedMessages);
    }
}