namespace ServiceControl.Audit.Persistence
{
    using System;
    using System.Collections.Generic;
    using System.Threading.Tasks;
    using Infrastructure;
    using ServiceControl.Audit.Auditing.MessagesView;
    using ServiceControl.Audit.Monitoring;
    using ServiceControl.SagaAudit;

    interface IAuditDataStore
    {
        Task<QueryResult<IList<KnownEndpointsView>>> QueryKnownEndpoints();
        Task<QueryResult<SagaHistory>> QuerySagaHistoryById(Guid input);
        Task<QueryResult<IList<MessagesView>>> GetMessages(bool includeSystemMessages, PagingInfo pagingInfo, SortInfo sortInfo);
        Task<QueryResult<IList<MessagesView>>> QueryMessages(string searchParam, PagingInfo pagingInfo, SortInfo sortInfo);
        Task<QueryResult<IList<MessagesView>>> QueryMessagesByReceivingEndpointAndKeyword(SearchEndpointApi.Input input, PagingInfo pagingInfo, SortInfo sortInfo);
        Task<QueryResult<IList<MessagesView>>> QueryMessagesByReceivingEndpoint(bool includeSystemMessages, string endpointName, PagingInfo pagingInfo, SortInfo sortInfo);
        Task<QueryResult<IList<MessagesView>>> QueryMessagesByConversationId(string conversationId, PagingInfo pagingInfo, SortInfo sortInfo);
        
        
        // This is being refactored anyway
        //Task<HttpResponseMessage> TryFetchFromIndex(HttpRequestMessage request, string messageId);
    }
}