namespace ServiceControl.Persistence.Tests.AuditCapable
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Threading;
    using System.Threading.Tasks;
    using ServiceControl.CompositeViews.Messages;
    using ServiceControl.Persistence.Infrastructure;

    // One local result set holding both failed and audited messages, merged under the precedence,
    // paging and counting rules IMessagesViewDataStore states.
    class AuditCapableMessagesViewDataStore(IMessagesViewDataStore inner, InMemoryAuditStore auditStore) : IMessagesViewDataStore
    {
        public async Task<QueryResult<IList<MessagesView>>> GetAllMessages(PagingInfo pagingInfo, SortInfo sortInfo, bool includeSystemMessages, DateTimeRange? timeSentRange = null, CancellationToken cancellationToken = default) =>
            Merge(await inner.GetAllMessages(pagingInfo, sortInfo, includeSystemMessages, timeSentRange, cancellationToken),
                Audited(includeSystemMessages, timeSentRange), pagingInfo, sortInfo);

        public async Task<QueryResult<IList<MessagesView>>> GetAllMessagesForEndpoint(string endpointName, PagingInfo pagingInfo, SortInfo sortInfo, bool includeSystemMessages, DateTimeRange? timeSentRange = null, CancellationToken cancellationToken = default) =>
            Merge(await inner.GetAllMessagesForEndpoint(endpointName, pagingInfo, sortInfo, includeSystemMessages, timeSentRange, cancellationToken),
                Audited(includeSystemMessages, timeSentRange).Where(message => message.ReceivingEndpoint?.Name == endpointName), pagingInfo, sortInfo);

        public async Task<QueryResult<IList<MessagesView>>> GetAllMessagesByConversation(string conversationId, PagingInfo pagingInfo, SortInfo sortInfo, bool includeSystemMessages, CancellationToken cancellationToken = default) =>
            Merge(await inner.GetAllMessagesByConversation(conversationId, pagingInfo, sortInfo, includeSystemMessages, cancellationToken),
                Audited(includeSystemMessages).Where(message => message.ConversationId == conversationId), pagingInfo, sortInfo);

        public async Task<QueryResult<IList<MessagesView>>> GetAllMessagesForSearch(string searchTerms, PagingInfo pagingInfo, SortInfo sortInfo, DateTimeRange? timeSentRange = null, CancellationToken cancellationToken = default) =>
            Merge(await inner.GetAllMessagesForSearch(searchTerms, pagingInfo, sortInfo, timeSentRange, cancellationToken),
                Audited(includeSystemMessages: true, timeSentRange).Where(message => Matches(message, searchTerms)), pagingInfo, sortInfo);

        public async Task<QueryResult<IList<MessagesView>>> SearchEndpointMessages(string endpointName, string searchKeyword, PagingInfo pagingInfo, SortInfo sortInfo, DateTimeRange? timeSentRange = null, CancellationToken cancellationToken = default) =>
            Merge(await inner.SearchEndpointMessages(endpointName, searchKeyword, pagingInfo, sortInfo, timeSentRange, cancellationToken),
                Audited(includeSystemMessages: true, timeSentRange)
                    .Where(message => message.ReceivingEndpoint?.Name == endpointName && Matches(message, searchKeyword)), pagingInfo, sortInfo);

        IEnumerable<MessagesView> Audited(bool includeSystemMessages, DateTimeRange? timeSentRange = null) =>
            auditStore.MessageViews
                .Where(message => includeSystemMessages || !message.IsSystemMessage)
                .Where(message => InRange(message, timeSentRange));

        static bool InRange(MessagesView message, DateTimeRange? timeSentRange) =>
            timeSentRange == null
            || (message.TimeSent >= timeSentRange.From && message.TimeSent <= timeSentRange.To);

        static bool Matches(MessagesView message, string searchTerms) =>
            searchTerms == null
            || (message.MessageType?.Contains(searchTerms, StringComparison.OrdinalIgnoreCase) ?? false)
            || (message.MessageId?.Contains(searchTerms, StringComparison.OrdinalIgnoreCase) ?? false);

        static QueryResult<IList<MessagesView>> Merge(QueryResult<IList<MessagesView>> failed, IEnumerable<MessagesView> audited, PagingInfo pagingInfo, SortInfo sortInfo) =>
            LocalMessagesView.Merge(
                [.. failed.Results ?? []],
                [.. audited],
                pagingInfo,
                MessageViewComparer.FromSortInfo(sortInfo),
                failed.QueryStats.ETag,
                failed.QueryStats.IsStale);
    }
}
