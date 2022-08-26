namespace ServiceControl.Audit.Persistence.InMemory
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Net;
    using System.Net.Http;
    using System.Threading.Tasks;
    using ServiceControl.Audit.Auditing.MessagesView;
    using ServiceControl.SagaAudit;
    using ServiceControl.Audit.Monitoring;
    using ServiceControl.Audit.Infrastructure;
    using NServiceBus.CustomChecks;
    using ServiceControl.Audit.Auditing;
    using System.Net.Http.Headers;

    class InMemoryAuditDataStore : IAuditDataStore
    {
        List<SagaHistory> sagaHistories;
        List<MessagesView> messageViews;
        List<ProcessedMessage> processedMessages;
        List<KnownEndpoint> knownEndpoints;
        List<FailedAuditImport> failedAuditImprots;

        public InMemoryAuditDataStore()
        {
            sagaHistories = new List<SagaHistory>();
            messageViews = new List<MessagesView>();
            processedMessages = new List<ProcessedMessage>();
            knownEndpoints = new List<KnownEndpoint>();
            failedAuditImprots = new List<FailedAuditImport>();
        }

        public async Task<QueryResult<SagaHistory>> QuerySagaHistoryById(Guid input)
        {
            var sagaHistory = sagaHistories.FirstOrDefault(w => w.SagaId == input);

            if (sagaHistory == null)
            {
                return await Task.FromResult(QueryResult<SagaHistory>.Empty()).ConfigureAwait(false);
            }

            return await Task.FromResult(new QueryResult<SagaHistory>(sagaHistory, new QueryStatsInfo(string.Empty, 1))).ConfigureAwait(false);
        }

        public async Task<QueryResult<IList<MessagesView>>> GetMessages(HttpRequestMessage request, PagingInfo pagingInfo)
        {
            return await Task.FromResult(new QueryResult<IList<MessagesView>>(messageViews, new QueryStatsInfo(string.Empty, messageViews.Count))).ConfigureAwait(false);
        }

        public async Task<QueryResult<IList<MessagesView>>> QueryMessages(HttpRequestMessage request, string searchParam, PagingInfo pagingInfo)
        {
            //TODO how should searchParam be used in this query?
            return await Task.FromResult(new QueryResult<IList<MessagesView>>(messageViews, new QueryStatsInfo(string.Empty, messageViews.Count))).ConfigureAwait(false);
        }

        public async Task<QueryResult<IList<MessagesView>>> QueryMessagesByReceivingEndpointAndKeyword(HttpRequestMessage request, SearchEndpointApi.Input input, PagingInfo pagingInfo)
        {
            //TODO how should input.Keyword be used in this query?
            var matched = messageViews.Where(w => w.ReceivingEndpoint.Name == input.Endpoint).ToList();
            return await Task.FromResult(new QueryResult<IList<MessagesView>>(matched, new QueryStatsInfo(string.Empty, matched.Count))).ConfigureAwait(false);
        }

        public async Task<QueryResult<IList<MessagesView>>> QueryMessagesByReceivingEndpoint(HttpRequestMessage request, string endpointName, PagingInfo pagingInfo)
        {
            var matched = messageViews.Where(w => w.ReceivingEndpoint.Name == endpointName).ToList();
            return await Task.FromResult(new QueryResult<IList<MessagesView>>(matched, new QueryStatsInfo(string.Empty, matched.Count))).ConfigureAwait(false);
        }

        public async Task<QueryResult<IList<MessagesView>>> QueryMessagesByConversationId(HttpRequestMessage request, string conversationId, PagingInfo pagingInfo)
        {
            var matched = messageViews.Where(w => w.ConversationId == conversationId).ToList();
            return await Task.FromResult(new QueryResult<IList<MessagesView>>(matched, new QueryStatsInfo(string.Empty, matched.Count))).ConfigureAwait(false);
        }

        public async Task<HttpResponseMessage> TryFetchFromIndex(HttpRequestMessage request, string messageId)
        {
            var message = processedMessages.FirstOrDefault(w => w.UniqueMessageId == messageId);

            if (message == null)
            {
                return request.CreateResponse(HttpStatusCode.NotFound);
            }

            var body = !string.IsNullOrEmpty(message.Body) ? message.Body : message.MessageMetadata["Body"];
            var bodySize = (int)message.MessageMetadata["ContentLength"];
            var contentType = message.MessageMetadata["ContentType"].ToString();
            var bodyNotStored = (bool)message.MessageMetadata["BodyNotStored"];

            if (bodyNotStored && message.Body == null)
            {
                return request.CreateResponse(HttpStatusCode.NoContent);
            }

            if (message.Body == null)
            {
                return request.CreateResponse(HttpStatusCode.NotFound);
            }

            var response = request.CreateResponse(HttpStatusCode.OK);
            var content = new StringContent(message.Body);

            MediaTypeHeaderValue.TryParse(contentType, out var parsedContentType);
            content.Headers.ContentType = parsedContentType ?? new MediaTypeHeaderValue("text/*");

            content.Headers.ContentLength = bodySize;
            response.Headers.ETag = new EntityTagHeaderValue($"\"{string.Empty}\"");
            response.Content = content;
            return await Task.FromResult(response).ConfigureAwait(false);
        }

        public async Task<QueryResult<IList<KnownEndpointsView>>> QueryKnownEndpoints()
        {
            var knownEndpointsView = knownEndpoints
                .Select(x => new KnownEndpointsView
                {
                    Id = DeterministicGuid.MakeId(x.Name, x.HostId.ToString()),
                    EndpointDetails = new EndpointDetails
                    {
                        Host = x.Host,
                        HostId = x.HostId,
                        Name = x.Name
                    },
                    HostDisplayName = x.Host
                })
                .ToList();

            return await Task.FromResult(new QueryResult<IList<KnownEndpointsView>>(knownEndpointsView, new QueryStatsInfo(string.Empty, knownEndpointsView.Count))).ConfigureAwait(false);
        }

        public Task MigrateEndpoints(int pageSize = 1024) => throw new NotImplementedException();

        public Task<CheckResult> PerformFailedAuditImportCheck(string errorMessage)
        {
            if (failedAuditImprots.Count > 0)
            {
                return CheckResult.Failed(errorMessage);
            }

            return CheckResult.Pass;
        }

        public Task SaveFailedAuditImport(FailedAuditImport message)
        {
            failedAuditImprots.Add(message);
            return Task.CompletedTask;
        }

        public Task Setup() => Task.CompletedTask;
    }
}