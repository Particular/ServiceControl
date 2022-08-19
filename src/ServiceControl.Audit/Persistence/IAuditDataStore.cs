namespace ServiceControl.Audit.Persistence
{
    using System;
    using System.Collections.Generic;
    using System.Net.Http;
    using System.Threading.Tasks;
    using NServiceBus.CustomChecks;
    using ServiceControl.Audit.Auditing;
    using ServiceControl.Audit.Auditing.MessagesView;
    using ServiceControl.Audit.Monitoring;
    using ServiceControl.SagaAudit;

    interface IAuditDataStore
    {
        Task<QueryResult<IList<KnownEndpointsView>>> QueryKnownEndpoints(HttpRequestMessage request);
        Task<QueryResult<SagaHistory>> QuerySagaHistoryById(HttpRequestMessage request, Guid input);
        Task<QueryResult<IList<MessagesView>>> GetMessages(HttpRequestMessage request);
        Task<QueryResult<IList<MessagesView>>> QueryMessages(HttpRequestMessage request, string searchParam);
        Task<QueryResult<IList<MessagesView>>> QueryMessagesByReceivingEndpointAndKeyword(HttpRequestMessage request, SearchEndpointApi.Input input);
        Task<QueryResult<IList<MessagesView>>> QueryMessagesByReceivingEndpoint(HttpRequestMessage request, string endpointName);
        Task<QueryResult<IList<MessagesView>>> QueryMessagesByConversationId(HttpRequestMessage request, string conversationId);
        Task<HttpResponseMessage> TryFetchFromIndex(HttpRequestMessage request, string messageId);
        Task MigrateEndpoints(int pageSize = 1024);

        Task<CheckResult> PerformFailedAuditImportCheck(string errorMessage);
        Task SaveFailedAuditImport(FailedAuditImport message);
    }
}