namespace ServiceControl.Audit.Persistence.InMemory
{
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Linq;
    using System.Net;
    using System.Net.Http;
    using System.Net.Http.Headers;
    using System.Threading.Tasks;
    using NServiceBus;
    using ServiceControl.Audit.Auditing;
    using ServiceControl.Audit.Auditing.BodyStorage;
    using ServiceControl.Audit.Auditing.MessagesView;
    using ServiceControl.Audit.Infrastructure;
    using ServiceControl.Audit.Monitoring;
    using ServiceControl.SagaAudit;

    class InMemoryAuditDataStore : IAuditDataStore
    {
        IBodyStorage bodyStorage;
        public List<KnownEndpoint> knownEndpoints;
        public List<FailedAuditImport> failedAuditImports;

        public InMemoryAuditDataStore(IBodyStorage bodyStorage)
        {
            this.bodyStorage = bodyStorage;
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
            var matched = messageViews
                .Where(w => !w.IsSystemMessage || includeSystemMessages)
                .ToList();

            return await Task.FromResult(new QueryResult<IList<MessagesView>>(matched, new QueryStatsInfo(string.Empty, matched.Count))).ConfigureAwait(false);
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

                    if (TryGet(pm.MessageMetadata, "MessageType") is string messageType && messageType.Contains(searchParam))
                    {
                        return true;
                    }

                    if (pm.Headers.Values.Contains(searchParam))
                    {
                        return true;
                    }

                    return pm.MessageMetadata.ContainsKey("Body") && (pm.MessageMetadata["Body"] as string).Contains(searchParam);
                })
                .Select(pm => pm.MessageMetadata["MessageId"] as string)
                .ToList();

            var matched = messageViews
                .Where(w => messages.Contains(w.MessageId))
                .ToList();
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

        public async Task<MessageBodyView> GetMessageBody(string messageId)
        {
            var result = await GetMessageBodyFromMetadata(messageId).ConfigureAwait(false);

            if (!result.Found)
            {
                var fromAttachments = await GetMessageBodyFromAttachments(messageId).ConfigureAwait(false);
                if (fromAttachments.Found)
                {
                    return fromAttachments;
                }
            }

            return result;
        }

        async Task<MessageBodyView> GetMessageBodyFromAttachments(string messageId)
        {
            var fromBodyStorage = await bodyStorage.TryFetch(messageId).ConfigureAwait(false);

            if (fromBodyStorage.HasResult)
            {
                return MessageBodyView.FromStream(
                    fromBodyStorage.Stream,
                    fromBodyStorage.ContentType,
                    fromBodyStorage.BodySize,
                    fromBodyStorage.Etag
                );
            }

            return MessageBodyView.NotFound();
        }

        Task<MessageBodyView> GetMessageBodyFromMetadata(string messageId)
        {
            var message = processedMessages.FirstOrDefault(pm => (pm.MessageMetadata["MessageId"] as string) == messageId);

            if (message == null)
            {
                return Task.FromResult(MessageBodyView.NotFound());
            }

            var body = !string.IsNullOrEmpty(message.Body) ? message.Body : TryGet(message.MessageMetadata, "Body") as string;
            var bodySize = (int)message.MessageMetadata["ContentLength"];
            var contentType = (string)message.MessageMetadata["ContentType"];
            var bodyNotStored = message.MessageMetadata.ContainsKey("BodyNotStored") && (bool)message.MessageMetadata["BodyNotStored"];

            if (bodyNotStored && body == null)
            {
                return Task.FromResult(MessageBodyView.NoContent());
            }

            if (body == null)
            {
                return Task.FromResult(MessageBodyView.NotFound());
            }

            return Task.FromResult(MessageBodyView.FromString(body, contentType, bodySize, string.Empty));
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

        public Task SaveProcessedMessage(ProcessedMessage processedMessage)
        {
            if (processedMessages.Any(pm => pm.UniqueMessageId == processedMessage.UniqueMessageId))
            {
                return Task.CompletedTask;
            }

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

        public Task SaveSagaSnapshot(SagaSnapshot sagaSnapshot)
        {
            var sagaHistory = sagaHistories.SingleOrDefault(sh => sh.SagaId == sagaSnapshot.SagaId);

            if (sagaHistory == null)
            {
                sagaHistory = new SagaHistory
                {
                    Id = sagaSnapshot.SagaId,
                    SagaId = sagaSnapshot.SagaId,
                    SagaType = sagaSnapshot.SagaType,
                };

                sagaHistories.Add(sagaHistory);
            }

            sagaHistory.Changes.Add(new SagaStateChange
            {
                StartTime = sagaSnapshot.StartTime,
                FinishTime = sagaSnapshot.FinishTime,
                Status = sagaSnapshot.Status,
                StateAfterChange = sagaSnapshot.StateAfterChange,
                InitiatingMessage = sagaSnapshot.InitiatingMessage,
                OutgoingMessages = sagaSnapshot.OutgoingMessages,
                Endpoint = sagaSnapshot.Endpoint
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
        List<SagaHistory> sagaHistories;
    }
}