namespace ServiceControl.Audit.Persistence.MongoDB
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Threading;
    using System.Threading.Tasks;
    using Auditing.BodyStorage;
    using Collections;
    using Documents;
    using global::MongoDB.Bson;
    using global::MongoDB.Driver;
    using NServiceBus;
    using ServiceControl.Audit.Auditing;
    using ServiceControl.Audit.Auditing.MessagesView;
    using ServiceControl.Audit.Infrastructure;
    using ServiceControl.Audit.Monitoring;
    using ServiceControl.Audit.Persistence.Infrastructure;
    using ServiceControl.SagaAudit;

    class MongoAuditDataStore(IMongoClientProvider clientProvider, IBodyStorage bodyStorage) : IAuditDataStore
    {
        public async Task<QueryResult<IList<MessagesView>>> GetMessages(
            bool includeSystemMessages,
            PagingInfo pagingInfo,
            SortInfo sortInfo,
            DateTimeRange timeSentRange,
            CancellationToken cancellationToken)
        {
            var collection = GetProcessedMessagesCollection();
            var filter = BuildMessageFilter(includeSystemMessages, timeSentRange);
            var sort = BuildSort(sortInfo);

            var totalCount = await collection.CountDocumentsAsync(filter, cancellationToken: cancellationToken).ConfigureAwait(false);

            var documents = await collection
                .Find(filter)
                .Sort(sort)
                .Skip(pagingInfo.Offset)
                .Limit(pagingInfo.PageSize)
                .ToListAsync(cancellationToken)
                .ConfigureAwait(false);

            var results = documents.Select(ToMessagesView).ToList();

            return new QueryResult<IList<MessagesView>>(results, new QueryStatsInfo(string.Empty, (int)totalCount));
        }

        public async Task<QueryResult<IList<MessagesView>>> QueryMessagesByReceivingEndpoint(
            bool includeSystemMessages,
            string endpointName,
            PagingInfo pagingInfo,
            SortInfo sortInfo,
            DateTimeRange timeSentRange,
            CancellationToken cancellationToken)
        {
            var collection = GetProcessedMessagesCollection();
            var endpointFilter = Builders<ProcessedMessageDocument>.Filter.Eq("messageMetadata.ReceivingEndpoint.Name", endpointName);
            var baseFilter = BuildMessageFilter(includeSystemMessages, timeSentRange);
            var filter = Builders<ProcessedMessageDocument>.Filter.And(endpointFilter, baseFilter);
            var sort = BuildSort(sortInfo);

            var totalCount = await collection.CountDocumentsAsync(filter, cancellationToken: cancellationToken).ConfigureAwait(false);

            var documents = await collection
                .Find(filter)
                .Sort(sort)
                .Skip(pagingInfo.Offset)
                .Limit(pagingInfo.PageSize)
                .ToListAsync(cancellationToken)
                .ConfigureAwait(false);

            var results = documents.Select(ToMessagesView).ToList();

            return new QueryResult<IList<MessagesView>>(results, new QueryStatsInfo(string.Empty, (int)totalCount));
        }

        public async Task<QueryResult<IList<MessagesView>>> QueryMessagesByConversationId(
            string conversationId,
            PagingInfo pagingInfo,
            SortInfo sortInfo,
            CancellationToken cancellationToken)
        {
            var collection = GetProcessedMessagesCollection();
            var filter = Builders<ProcessedMessageDocument>.Filter.Eq("messageMetadata.ConversationId", conversationId);
            var sort = BuildSort(sortInfo);

            var totalCount = await collection.CountDocumentsAsync(filter, cancellationToken: cancellationToken).ConfigureAwait(false);

            var documents = await collection
                .Find(filter)
                .Sort(sort)
                .Skip(pagingInfo.Offset)
                .Limit(pagingInfo.PageSize)
                .ToListAsync(cancellationToken)
                .ConfigureAwait(false);

            var results = documents.Select(ToMessagesView).ToList();

            return new QueryResult<IList<MessagesView>>(results, new QueryStatsInfo(string.Empty, (int)totalCount));
        }

        public async Task<QueryResult<IList<MessagesView>>> QueryMessages(
            string searchParam,
            PagingInfo pagingInfo,
            SortInfo sortInfo,
            DateTimeRange timeSentRange,
            CancellationToken cancellationToken)
        {
            var collection = GetProcessedMessagesCollection();

            // Combine text search with time range filter
            var textFilter = Builders<ProcessedMessageDocument>.Filter.Text(searchParam);
            var timeRangeFilter = BuildTimeSentRangeFilter(timeSentRange);
            var filter = Builders<ProcessedMessageDocument>.Filter.And(textFilter, timeRangeFilter);

            var sort = BuildSort(sortInfo);

            var totalCount = await collection.CountDocumentsAsync(filter, cancellationToken: cancellationToken).ConfigureAwait(false);

            var documents = await collection
                .Find(filter)
                .Sort(sort)
                .Skip(pagingInfo.Offset)
                .Limit(pagingInfo.PageSize)
                .ToListAsync(cancellationToken)
                .ConfigureAwait(false);

            var results = documents.Select(ToMessagesView).ToList();

            return new QueryResult<IList<MessagesView>>(results, new QueryStatsInfo(string.Empty, (int)totalCount));
        }

        public async Task<QueryResult<IList<MessagesView>>> QueryMessagesByReceivingEndpointAndKeyword(
            string endpoint,
            string keyword,
            PagingInfo pagingInfo,
            SortInfo sortInfo,
            DateTimeRange timeSentRange,
            CancellationToken cancellationToken)
        {
            var collection = GetProcessedMessagesCollection();

            // Combine text search, endpoint filter, and time range filter
            var textFilter = Builders<ProcessedMessageDocument>.Filter.Text(keyword);
            var endpointFilter = Builders<ProcessedMessageDocument>.Filter.Eq("messageMetadata.ReceivingEndpoint.Name", endpoint);
            var timeRangeFilter = BuildTimeSentRangeFilter(timeSentRange);
            var filter = Builders<ProcessedMessageDocument>.Filter.And(textFilter, endpointFilter, timeRangeFilter);

            var sort = BuildSort(sortInfo);

            var totalCount = await collection.CountDocumentsAsync(filter, cancellationToken: cancellationToken).ConfigureAwait(false);

            var documents = await collection
                .Find(filter)
                .Sort(sort)
                .Skip(pagingInfo.Offset)
                .Limit(pagingInfo.PageSize)
                .ToListAsync(cancellationToken)
                .ConfigureAwait(false);

            var results = documents.Select(ToMessagesView).ToList();

            return new QueryResult<IList<MessagesView>>(results, new QueryStatsInfo(string.Empty, (int)totalCount));
        }

        public async Task<QueryResult<IList<KnownEndpointsView>>> QueryKnownEndpoints(CancellationToken cancellationToken)
        {
            var collection = clientProvider.Database.GetCollection<KnownEndpointDocument>(CollectionNames.KnownEndpoints);

            var documents = await collection
                .Find(FilterDefinition<KnownEndpointDocument>.Empty)
                .Limit(1024)
                .ToListAsync(cancellationToken)
                .ConfigureAwait(false);

            var results = documents.Select(doc => new KnownEndpointsView
            {
                Id = DeterministicGuid.MakeId(doc.Name, doc.HostId.ToString()),
                EndpointDetails = new EndpointDetails
                {
                    Host = doc.Host,
                    HostId = doc.HostId,
                    Name = doc.Name
                },
                HostDisplayName = doc.Host
            }).ToList();

            return new QueryResult<IList<KnownEndpointsView>>(results, new QueryStatsInfo(string.Empty, results.Count));
        }

        public async Task<QueryResult<SagaHistory>> QuerySagaHistoryById(Guid sagaId, CancellationToken cancellationToken)
        {
            var collection = clientProvider.Database.GetCollection<SagaSnapshotDocument>(CollectionNames.SagaSnapshots);

            var snapshots = await collection
                .Find(doc => doc.SagaId == sagaId)
                .SortBy(doc => doc.StartTime)
                .ToListAsync(cancellationToken)
                .ConfigureAwait(false);

            if (snapshots.Count == 0)
            {
                return QueryResult<SagaHistory>.Empty();
            }

            var sagaHistory = new SagaHistory
            {
                Id = sagaId,
                SagaId = sagaId,
                SagaType = snapshots[0].SagaType,
                Changes = [.. snapshots.Select(snapshot => new SagaStateChange
                {
                    StartTime = snapshot.StartTime,
                    FinishTime = snapshot.FinishTime,
                    Status = snapshot.Status,
                    StateAfterChange = snapshot.StateAfterChange,
                    Endpoint = snapshot.Endpoint,
                    InitiatingMessage = snapshot.InitiatingMessage != null
                        ? new InitiatingMessage
                        {
                            MessageId = snapshot.InitiatingMessage.MessageId,
                            MessageType = snapshot.InitiatingMessage.MessageType,
                            IsSagaTimeoutMessage = snapshot.InitiatingMessage.IsSagaTimeoutMessage,
                            OriginatingMachine = snapshot.InitiatingMessage.OriginatingMachine,
                            OriginatingEndpoint = snapshot.InitiatingMessage.OriginatingEndpoint,
                            TimeSent = snapshot.InitiatingMessage.TimeSent,
                            Intent = snapshot.InitiatingMessage.Intent
                        }
                        : null,
                    OutgoingMessages = snapshot.OutgoingMessages?.Select(msg => new ResultingMessage
                    {
                        MessageId = msg.MessageId,
                        MessageType = msg.MessageType,
                        Destination = msg.Destination,
                        TimeSent = msg.TimeSent,
                        Intent = msg.Intent,
                        DeliveryDelay = !string.IsNullOrEmpty(msg.DeliveryDelay) ? TimeSpan.Parse(msg.DeliveryDelay) : null,
                        DeliverAt = msg.DeliverAt
                    }).ToList() ?? []
                })]
            };

            return new QueryResult<SagaHistory>(sagaHistory, new QueryStatsInfo(string.Empty, 1));
        }

        public async Task<QueryResult<IList<AuditCount>>> QueryAuditCounts(string endpointName, CancellationToken cancellationToken)
        {
            var collection = GetProcessedMessagesCollection();
            var results = new List<AuditCount>();

            // Find oldest message for this endpoint
            var endpointFilter = Builders<ProcessedMessageDocument>.Filter.Eq("messageMetadata.ReceivingEndpoint.Name", endpointName);

            var oldestMsg = await collection
                .Find(endpointFilter)
                .Sort(Builders<ProcessedMessageDocument>.Sort.Ascending(x => x.ProcessedAt))
                .Limit(1)
                .FirstOrDefaultAsync(cancellationToken)
                .ConfigureAwait(false);

            if (oldestMsg == null)
            {
                return new QueryResult<IList<AuditCount>>(results, QueryStatsInfo.Zero);
            }

            var endDate = DateTime.UtcNow.Date.AddDays(1);
            var oldestMsgDate = oldestMsg.ProcessedAt.ToUniversalTime().Date;
            var thirtyDaysAgo = endDate.AddDays(-30);

            var startDate = oldestMsgDate > thirtyDaysAgo ? oldestMsgDate : thirtyDaysAgo;

            // Query each day - similar to RavenDB implementation
            for (var date = startDate; date < endDate; date = date.AddDays(1))
            {
                var nextDate = date.AddDays(1);

                var dayFilter = Builders<ProcessedMessageDocument>.Filter.And(
                    Builders<ProcessedMessageDocument>.Filter.Eq("messageMetadata.ReceivingEndpoint.Name", endpointName),
                    Builders<ProcessedMessageDocument>.Filter.Eq("messageMetadata.IsSystemMessage", false),
                    Builders<ProcessedMessageDocument>.Filter.Gte(x => x.ProcessedAt, date),
                    Builders<ProcessedMessageDocument>.Filter.Lt(x => x.ProcessedAt, nextDate)
                );

                var count = await collection.CountDocumentsAsync(dayFilter, cancellationToken: cancellationToken).ConfigureAwait(false);

                if (count > 0)
                {
                    results.Add(new AuditCount { UtcDate = date, Count = count });
                }
            }

            return new QueryResult<IList<AuditCount>>(results, QueryStatsInfo.Zero);
        }

        public async Task<MessageBodyView> GetMessageBody(string messageId, CancellationToken cancellationToken)
        {
            var result = await bodyStorage.TryFetch(messageId, cancellationToken).ConfigureAwait(false);

            if (result.HasResult)
            {
                return MessageBodyView.FromStream(
                    result.Stream,
                    result.ContentType,
                    result.BodySize,
                    result.Etag
                );
            }

            return MessageBodyView.NotFound();
        }

        IMongoCollection<ProcessedMessageDocument> GetProcessedMessagesCollection()
            => clientProvider.Database.GetCollection<ProcessedMessageDocument>(CollectionNames.ProcessedMessages);

        static FilterDefinition<ProcessedMessageDocument> BuildMessageFilter(bool includeSystemMessages, DateTimeRange timeSentRange)
        {
            var filters = new List<FilterDefinition<ProcessedMessageDocument>>();

            if (!includeSystemMessages)
            {
                filters.Add(Builders<ProcessedMessageDocument>.Filter.Ne("messageMetadata.IsSystemMessage", true));
            }

            if (timeSentRange?.From != null)
            {
                filters.Add(Builders<ProcessedMessageDocument>.Filter.Gte("messageMetadata.TimeSent", timeSentRange.From.Value));
            }

            if (timeSentRange?.To != null)
            {
                filters.Add(Builders<ProcessedMessageDocument>.Filter.Lte("messageMetadata.TimeSent", timeSentRange.To.Value));
            }

            return filters.Count > 0
                ? Builders<ProcessedMessageDocument>.Filter.And(filters)
                : FilterDefinition<ProcessedMessageDocument>.Empty;
        }

        static FilterDefinition<ProcessedMessageDocument> BuildTimeSentRangeFilter(DateTimeRange timeSentRange)
        {
            var filters = new List<FilterDefinition<ProcessedMessageDocument>>();

            if (timeSentRange?.From != null)
            {
                filters.Add(Builders<ProcessedMessageDocument>.Filter.Gte("messageMetadata.TimeSent", timeSentRange.From.Value));
            }

            if (timeSentRange?.To != null)
            {
                filters.Add(Builders<ProcessedMessageDocument>.Filter.Lte("messageMetadata.TimeSent", timeSentRange.To.Value));
            }

            return filters.Count > 0
                ? Builders<ProcessedMessageDocument>.Filter.And(filters)
                : FilterDefinition<ProcessedMessageDocument>.Empty;
        }

        static SortDefinition<ProcessedMessageDocument> BuildSort(SortInfo sortInfo)
        {
            var sortField = sortInfo?.Sort?.ToLowerInvariant() switch
            {
                "time_sent" => "messageMetadata.TimeSent",
                _ => "processedAt"
            };

            var isDescending = sortInfo?.Direction?.ToLowerInvariant() != "asc";

            return isDescending
                ? Builders<ProcessedMessageDocument>.Sort.Descending(sortField)
                : Builders<ProcessedMessageDocument>.Sort.Ascending(sortField);
        }

        static MessagesView ToMessagesView(ProcessedMessageDocument doc)
        {
            var metadata = doc.MessageMetadata;

            return new MessagesView
            {
                Id = doc.Id,
                MessageId = GetMetadataString(metadata, "MessageId"),
                MessageType = GetMetadataString(metadata, "MessageType"),
                TimeSent = GetMetadataDateTime(metadata, "TimeSent"),
                ProcessedAt = doc.ProcessedAt,
                CriticalTime = GetMetadataTimeSpan(metadata, "CriticalTime"),
                ProcessingTime = GetMetadataTimeSpan(metadata, "ProcessingTime"),
                DeliveryTime = GetMetadataTimeSpan(metadata, "DeliveryTime"),
                IsSystemMessage = GetMetadataBool(metadata, "IsSystemMessage"),
                ConversationId = GetMetadataString(metadata, "ConversationId"),
                ReceivingEndpoint = GetMetadataEndpoint(metadata, "ReceivingEndpoint"),
                SendingEndpoint = GetMetadataEndpoint(metadata, "SendingEndpoint"),
                Headers = doc.Headers?.Select(h => new KeyValuePair<string, string>(h.Key, h.Value)),
                BodyUrl = GetMetadataString(metadata, "BodyUrl"),
                BodySize = GetMetadataInt(metadata, "ContentLength"),
                Status = MessageStatus.Successful,
                MessageIntent = GetMetadataMessageIntent(metadata, "MessageIntent"),
                InvokedSagas = GetMetadataSagaInfoList(metadata, "InvokedSagas"),
                OriginatesFromSaga = GetMetadataSagaInfo(metadata, "OriginatesFromSaga")
            };
        }

        static string GetMetadataString(BsonDocument metadata, string key)
        {
            if (metadata == null || !metadata.TryGetValue(key, out var value) || value.IsBsonNull)
            {
                return null;
            }

            return value.AsString;
        }

        static DateTime? GetMetadataDateTime(BsonDocument metadata, string key)
        {
            if (metadata == null || !metadata.TryGetValue(key, out var value) || value.IsBsonNull)
            {
                return null;
            }

            return value.ToUniversalTime();
        }

        static bool GetMetadataBool(BsonDocument metadata, string key)
        {
            if (metadata == null || !metadata.TryGetValue(key, out var value) || value.IsBsonNull)
            {
                return false;
            }

            return value.AsBoolean;
        }

        static int GetMetadataInt(BsonDocument metadata, string key)
        {
            if (metadata == null || !metadata.TryGetValue(key, out var value) || value.IsBsonNull)
            {
                return 0;
            }

            return value.ToInt32();
        }

        static TimeSpan GetMetadataTimeSpan(BsonDocument metadata, string key)
        {
            if (metadata == null || !metadata.TryGetValue(key, out var value) || value.IsBsonNull)
            {
                return TimeSpan.Zero;
            }

            if (value.IsString && TimeSpan.TryParse(value.AsString, out var result))
            {
                return result;
            }

            return TimeSpan.Zero;
        }

        static EndpointDetails GetMetadataEndpoint(BsonDocument metadata, string key)
        {
            if (metadata == null || !metadata.TryGetValue(key, out var value) || value.IsBsonNull || !value.IsBsonDocument)
            {
                return null;
            }

            var endpointDoc = value.AsBsonDocument;

            return new EndpointDetails
            {
                Name = endpointDoc.TryGetValue("Name", out var name) && !name.IsBsonNull ? name.AsString : null,
                Host = endpointDoc.TryGetValue("Host", out var host) && !host.IsBsonNull ? host.AsString : null,
                HostId = endpointDoc.TryGetValue("HostId", out var hostId) && !hostId.IsBsonNull ? BsonValueToGuid(hostId) : Guid.Empty
            };
        }

        static MessageIntent GetMetadataMessageIntent(BsonDocument metadata, string key)
        {
            if (metadata == null || !metadata.TryGetValue(key, out var value) || value.IsBsonNull)
            {
                return MessageIntent.Send;
            }

            if (value.IsString && Enum.TryParse<MessageIntent>(value.AsString, out var result))
            {
                return result;
            }

            if (value.IsInt32)
            {
                return (MessageIntent)value.AsInt32;
            }

            return MessageIntent.Send;
        }

        static List<SagaInfo> GetMetadataSagaInfoList(BsonDocument metadata, string key)
        {
            if (metadata == null || !metadata.TryGetValue(key, out var value) || value.IsBsonNull || !value.IsBsonArray)
            {
                return null;
            }

            return value.AsBsonArray
                .Where(v => v.IsBsonDocument)
                .Select(v => MapSagaInfo(v.AsBsonDocument))
                .Where(s => s != null)
                .ToList();
        }

        static SagaInfo GetMetadataSagaInfo(BsonDocument metadata, string key)
        {
            if (metadata == null || !metadata.TryGetValue(key, out var value) || value.IsBsonNull || !value.IsBsonDocument)
            {
                return null;
            }

            return MapSagaInfo(value.AsBsonDocument);
        }

        static SagaInfo MapSagaInfo(BsonDocument doc)
        {
            if (doc == null)
            {
                return null;
            }

            return new SagaInfo
            {
                SagaId = doc.TryGetValue("SagaId", out var sagaId) && !sagaId.IsBsonNull ? BsonValueToGuid(sagaId) : Guid.Empty,
                SagaType = doc.TryGetValue("SagaType", out var sagaType) && !sagaType.IsBsonNull ? sagaType.AsString : null,
                ChangeStatus = doc.TryGetValue("ChangeStatus", out var changeStatus) && !changeStatus.IsBsonNull ? changeStatus.AsString : null
            };
        }

        // Handles Guids stored as either strings (from metadata) or BsonBinaryData (from document properties)
        static Guid BsonValueToGuid(BsonValue value)
        {
            if (value.IsString)
            {
                return Guid.Parse(value.AsString);
            }
            return value.AsGuid;
        }
    }
}
