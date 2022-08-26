namespace ServiceControl.Audit.Persistence
{
    using System;
    using System.Collections.Generic;
    using System.Net.Http;
    using System.Threading.Tasks;
    using Infrastructure;
    using NServiceBus.CustomChecks;
    using ServiceControl.Audit.Auditing;
    using ServiceControl.Audit.Auditing.MessagesView;
    using ServiceControl.Audit.Monitoring;
    using ServiceControl.SagaAudit;

    interface IAuditDataStore
    {
        Task<QueryResult<IList<KnownEndpointsView>>> QueryKnownEndpoints();
        Task<QueryResult<SagaHistory>> QuerySagaHistoryById(Guid input);
        Task<QueryResult<IList<MessagesView>>> GetMessages(HttpRequestMessage request, PagingInfo pagingInfo, SortInfo sortInfo);
        Task<QueryResult<IList<MessagesView>>> QueryMessages(HttpRequestMessage request, string searchParam, PagingInfo pagingInfo, SortInfo sortInfo);
        Task<QueryResult<IList<MessagesView>>> QueryMessagesByReceivingEndpointAndKeyword(HttpRequestMessage request, SearchEndpointApi.Input input, PagingInfo pagingInfo, SortInfo sortInfo);
        Task<QueryResult<IList<MessagesView>>> QueryMessagesByReceivingEndpoint(HttpRequestMessage request, string endpointName, PagingInfo pagingInfo, SortInfo sortInfo);
        Task<QueryResult<IList<MessagesView>>> QueryMessagesByConversationId(HttpRequestMessage request, string conversationId, PagingInfo pagingInfo, SortInfo sortInfo);
        Task<HttpResponseMessage> TryFetchFromIndex(HttpRequestMessage request, string messageId);
        Task MigrateEndpoints(int pageSize = 1024);

        Task<CheckResult> PerformFailedAuditImportCheck(string errorMessage);
        Task SaveFailedAuditImport(FailedAuditImport message);
    }
}