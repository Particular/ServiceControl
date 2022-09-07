namespace ServiceControl.Audit.Persistence.InMemory
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Net;
    using System.Net.Http;
    using System.Net.Http.Headers;
    using System.Threading.Tasks;
    using NServiceBus;
    using NServiceBus.CustomChecks;
    using ServiceControl.Audit.Auditing;
    using ServiceControl.Audit.Auditing.MessagesView;
    using ServiceControl.Audit.Infrastructure;
    using ServiceControl.Audit.Monitoring;
    using ServiceControl.SagaAudit;

    class InMemoryAuditDataStore : IAuditDataStore
    {
        public List<SagaHistory> sagaHistories;
        public List<KnownEndpoint> knownEndpoints;
        public List<FailedAuditImport> failedAuditImports;

        public InMemoryAuditDataStore()
        {
            sagaHistories = new List<SagaHistory>();
            messageViews = new List<MessagesView>();
            processedMessages = new List<ProcessedMessage>();
            knownEndpoints = new List<KnownEndpoint>();
            failedAuditImports = new List<FailedAuditImport>();
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

        public async Task<QueryResult<IList<MessagesView>>> GetMessages(bool includeSystemMessages, PagingInfo pagingInfo, SortInfo sortInfo)
        {
            return await Task.FromResult(new QueryResult<IList<MessagesView>>(messageViews, new QueryStatsInfo(string.Empty, messageViews.Count))).ConfigureAwait(false);
        }

        public async Task<QueryResult<IList<MessagesView>>> QueryMessages(string searchParam, PagingInfo pagingInfo, SortInfo sortInfo)
        {
            var messages = processedMessages
                .Where(pm =>
                {
                    if ((pm.MessageMetadata["MessageId"] as string) == searchParam)
                    {
                        return true;
                    }

                    return pm.MessageMetadata.ContainsKey("Body") && (pm.MessageMetadata["Body"] as string).Contains(searchParam);
                })
                .Select(pm => pm.MessageMetadata["MessageId"] as string)
                .ToList();

            var matched = messageViews.Where(w => messages.Contains(w.MessageId)).ToList();
            return await Task.FromResult(new QueryResult<IList<MessagesView>>(matched, new QueryStatsInfo(string.Empty, matched.Count()))).ConfigureAwait(false);
        }

        public async Task<QueryResult<IList<MessagesView>>> QueryMessagesByReceivingEndpointAndKeyword(SearchEndpointApi.Input input, PagingInfo pagingInfo, SortInfo sortInfo)
        {
            //TODO how should input.Keyword be used in this query?
            var matched = messageViews.Where(w => w.ReceivingEndpoint.Name == input.Endpoint).ToList();
            return await Task.FromResult(new QueryResult<IList<MessagesView>>(matched, new QueryStatsInfo(string.Empty, matched.Count))).ConfigureAwait(false);
        }

        public async Task<QueryResult<IList<MessagesView>>> QueryMessagesByReceivingEndpoint(bool includeSystemMessages, string endpointName, PagingInfo pagingInfo, SortInfo sortInfo)
        {
            var matched = messageViews.Where(w => w.ReceivingEndpoint.Name == endpointName).ToList();
            return await Task.FromResult(new QueryResult<IList<MessagesView>>(matched, new QueryStatsInfo(string.Empty, matched.Count))).ConfigureAwait(false);
        }

        public async Task<QueryResult<IList<MessagesView>>> QueryMessagesByConversationId(string conversationId, PagingInfo pagingInfo, SortInfo sortInfo)
        {
            var matched = messageViews.Where(w => w.ConversationId == conversationId).ToList();
            return await Task.FromResult(new QueryResult<IList<MessagesView>>(matched, new QueryStatsInfo(string.Empty, matched.Count))).ConfigureAwait(false);
        }

        public async Task<HttpResponseMessage> TryFetchFromIndex(HttpRequestMessage request, string messageId)
        {
            var message = processedMessages.FirstOrDefault(pm => (pm.MessageMetadata["MessageId"] as string) == messageId);

            if (message == null)
            {
                return request.CreateResponse(HttpStatusCode.NotFound);
            }

            var body = !string.IsNullOrEmpty(message.Body) ? message.Body : TryGet(message.MessageMetadata, "Body") as string;
            var bodySize = (int)message.MessageMetadata["ContentLength"];
            var contentType = (string)message.MessageMetadata["ContentType"];
            var bodyNotStored = message.MessageMetadata.ContainsKey("BodyNotStored") && (bool)message.MessageMetadata["BodyNotStored"];

            if (bodyNotStored && body == null)
            {
                return request.CreateResponse(HttpStatusCode.NoContent);
            }

            if (body == null)
            {
                return request.CreateResponse(HttpStatusCode.NotFound);
            }

            var response = request.CreateResponse(HttpStatusCode.OK);
            var content = new StringContent(body);

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

        public Task<CheckResult> PerformFailedAuditImportCheck(string errorMessage)
        {
            if (failedAuditImports.Count > 0)
            {
                return CheckResult.Failed(errorMessage);
            }

            return CheckResult.Pass;
        }

        public Task SaveFailedAuditImport(FailedAuditImport message)
        {
            failedAuditImports.Add(message);
            return Task.CompletedTask;
        }

        public Task SaveProcessedMessage(ProcessedMessage processedMessage)
        {
            processedMessages.Add(processedMessage);
            var metadata = processedMessage.MessageMetadata;
            var headers = processedMessage.Headers;

            messageViews.Add(new MessagesView
            {
                Id = processedMessage.UniqueMessageId,
                MessageId = (string)metadata["MessageId"],
                MessageType = (string)metadata["MessageType"],
                SendingEndpoint = TryGet(metadata, "SendingEndpoint") as EndpointDetails,
                ReceivingEndpoint = TryGet(metadata, "ReceivingEndpoint") as EndpointDetails,
                TimeSent = TryGet(metadata, "TimeSent") as DateTime?,
                ProcessedAt = processedMessage.ProcessedAt,
                CriticalTime = (TimeSpan)metadata["CriticalTime"],
                ProcessingTime = (TimeSpan)metadata["ProcessingTime"],
                DeliveryTime = (TimeSpan)metadata["DeliveryTime"],
                IsSystemMessage = (bool)metadata["IsSystemMessage"],
                ConversationId = TryGet(metadata, "ConversationId") as string,
                Headers = headers.Select(header => new KeyValuePair<string, object>(header.Key, header.Value)),
                Status = !(bool)metadata["IsRetried"] ? MessageStatus.Successful : MessageStatus.ResolvedSuccessfully,
                MessageIntent = (MessageIntentEnum)metadata["MessageIntent"],
                BodyUrl = TryGet(metadata, "BodyUrl") as string,
                BodySize = (int)metadata["ContentLength"],
                InvokedSagas = TryGet(metadata, "InvokedSagas") as List<SagaInfo>,
                OriginatesFromSaga = TryGet(metadata, "OriginatesFromSaga") as SagaInfo
            });

            return Task.CompletedTask;
        }
        object TryGet(Dictionary<string, object> metadata, string key)
        {
            if (metadata.TryGetValue(key, out var value))
            {
                return value;
            }

            return null;
        }
        public Task Setup() => Task.CompletedTask;

        List<MessagesView> messageViews;
        List<ProcessedMessage> processedMessages;
    }
}