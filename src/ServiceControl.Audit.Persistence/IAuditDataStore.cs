namespace ServiceControl.Audit.Persistence
{
    using System;
    using System.Collections.Generic;
    using System.Threading.Tasks;
    using Audit.Infrastructure;
    using Audit.Monitoring;
    using Auditing;
    using Auditing.MessagesView;
    using SagaAudit;

    public interface IAuditDataStore
    {
        Task<QueryResult<IList<KnownEndpointsView>>> QueryKnownEndpoints();
        Task<QueryResult<SagaHistory>> QuerySagaHistoryById(Guid input);
        Task<QueryResult<IList<MessagesView>>> GetMessages(bool includeSystemMessages, PagingInfo pagingInfo, SortInfo sortInfo);
        Task<QueryResult<IList<MessagesView>>> QueryMessages(string searchParam, PagingInfo pagingInfo, SortInfo sortInfo);
        Task<QueryResult<IList<MessagesView>>> QueryMessagesByReceivingEndpointAndKeyword(string endpoint, string keyword, PagingInfo pagingInfo, SortInfo sortInfo);

        Task<QueryResult<IList<MessagesView>>> QueryMessagesByReceivingEndpointAndProcessedAt(string endpoint,
            DateTime startDate, DateTime endDate, PagingInfo pagingInfo, SortInfo sortInfo);
        Task<QueryResult<IList<MessagesView>>> QueryMessagesByReceivingEndpoint(bool includeSystemMessages, string endpointName, PagingInfo pagingInfo, SortInfo sortInfo);
        Task<QueryResult<IList<MessagesView>>> QueryMessagesByConversationId(string conversationId, PagingInfo pagingInfo, SortInfo sortInfo);
        Task<MessageBodyView> GetMessageBody(string messageId);
        Task<QueryResult<IList<AuditCount>>> QueryAuditCounts(string endpointName);
    }
}