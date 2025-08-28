
namespace ServiceControl.Audit.Persistence.PostgreSQL;

using System;
using System.Collections.Generic;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Npgsql;
using NServiceBus;
using ServiceControl.Audit.Auditing;
using ServiceControl.Audit.Auditing.MessagesView;
using ServiceControl.Audit.Infrastructure;
using ServiceControl.Audit.Monitoring;
using ServiceControl.Audit.Persistence;
using ServiceControl.SagaAudit;


class PostgreSQLAuditDataStore(PostgreSQLConnectionFactory connectionFactory) : IAuditDataStore
{
    public async Task<MessageBodyView> GetMessageBody(string messageId, CancellationToken cancellationToken)
    {
        using var conn = await connectionFactory.OpenConnection(cancellationToken);
        using var cmd = new NpgsqlCommand(@"
                select body, headers from processed_messages
                where message_id = @message_id
                LIMIT 1;", conn);
        cmd.Parameters.AddWithValue("message_id", messageId);
        using var reader = await cmd.ExecuteReaderAsync(cancellationToken);
        if (await reader.ReadAsync(cancellationToken))
        {
            var stream = await reader.GetStreamAsync(reader.GetOrdinal("body"), cancellationToken);
            var contentType = reader.GetFieldValue<Dictionary<string, string>>(reader.GetOrdinal("headers")).GetValueOrDefault(Headers.ContentType, "text/xml");
            return MessageBodyView.FromStream(stream, contentType, (int)stream.Length, string.Empty);
        }
        return MessageBodyView.NotFound();
    }

    public Task<QueryResult<IList<MessagesView>>> GetMessages(bool includeSystemMessages, PagingInfo pagingInfo, SortInfo sortInfo, DateTimeRange timeSentRange, CancellationToken cancellationToken)
    {
        var builder = new PostgresqlMessagesQueryBuilder()
            .WithSystemMessages(includeSystemMessages)
            .WithTimeSentRange(timeSentRange)
            .WithSorting(sortInfo)
            .WithPaging(pagingInfo);
        return ExecuteMessagesQuery(builder, cancellationToken);
    }

    public Task<QueryResult<IList<AuditCount>>> QueryAuditCounts(string endpointName, CancellationToken cancellationToken) => throw new NotImplementedException();
    public Task<QueryResult<IList<KnownEndpointsView>>> QueryKnownEndpoints(CancellationToken cancellationToken) => throw new NotImplementedException();
    public Task<QueryResult<IList<MessagesView>>> QueryMessages(string searchParam, PagingInfo pagingInfo, SortInfo sortInfo, DateTimeRange? timeSentRange = null, CancellationToken cancellationToken = default)
    {
        var builder = new PostgresqlMessagesQueryBuilder()
            .WithSearch(searchParam)
            .WithTimeSentRange(timeSentRange)
            .WithSorting(sortInfo)
            .WithPaging(pagingInfo);
        return ExecuteMessagesQuery(builder, cancellationToken);
    }

    public Task<QueryResult<IList<MessagesView>>> QueryMessagesByConversationId(string conversationId, PagingInfo pagingInfo, SortInfo sortInfo, CancellationToken cancellationToken)
    {
        var builder = new PostgresqlMessagesQueryBuilder()
            .WithConversationId(conversationId)
            .WithSorting(sortInfo)
            .WithPaging(pagingInfo);
        return ExecuteMessagesQuery(builder, cancellationToken);
    }

    public Task<QueryResult<IList<MessagesView>>> QueryMessagesByReceivingEndpoint(bool includeSystemMessages, string endpointName, PagingInfo pagingInfo, SortInfo sortInfo, DateTimeRange? timeSentRange = null, CancellationToken cancellationToken = default)
    {
        var builder = new PostgresqlMessagesQueryBuilder()
            .WithSystemMessages(includeSystemMessages)
            .WithEndpointName(endpointName)
            .WithTimeSentRange(timeSentRange)
            .WithSorting(sortInfo)
            .WithPaging(pagingInfo);
        return ExecuteMessagesQuery(builder, cancellationToken);
    }

    public Task<QueryResult<IList<MessagesView>>> QueryMessagesByReceivingEndpointAndKeyword(string endpoint, string keyword, PagingInfo pagingInfo, SortInfo sortInfo, DateTimeRange? timeSentRange = null, CancellationToken cancellationToken = default)
    {
        var builder = new PostgresqlMessagesQueryBuilder()
            .WithSearch(keyword)
            .WithEndpointName(endpoint)
            .WithTimeSentRange(timeSentRange)
            .WithSorting(sortInfo)
            .WithPaging(pagingInfo);
        return ExecuteMessagesQuery(builder, cancellationToken);
    }

    public Task<QueryResult<SagaHistory>> QuerySagaHistoryById(Guid input, CancellationToken cancellationToken) => throw new NotImplementedException();

    async Task<QueryResult<IList<MessagesView>>> ExecuteMessagesQuery(
        PostgresqlMessagesQueryBuilder builder,
        CancellationToken cancellationToken)
    {
        using var conn = await connectionFactory.OpenConnection(cancellationToken);
        var (query, parameters) = builder.Build();
        using var cmd = new NpgsqlCommand(query, conn);
        foreach (var param in parameters)
        {
            cmd.Parameters.Add(param);
        }
        return await ReturnResults(cmd, cancellationToken);
    }

    static T? DeserializeOrDefault<T>(Dictionary<string, object> dict, string key, T? defaultValue = default)
    {
        if (dict.TryGetValue(key, out var value) && value is JsonElement element && element.ValueKind != JsonValueKind.Null)
        {
            try
            {
                return JsonSerializer.Deserialize<T>(element);
            }
            catch { }
        }
        return defaultValue;
    }

    static T GetValue<T>(NpgsqlDataReader reader, string column)
        => reader.GetFieldValue<T>(reader.GetOrdinal(column));

    async Task<QueryResult<IList<MessagesView>>> ReturnResults(NpgsqlCommand cmd, CancellationToken cancellationToken = default)
    {
        var results = new List<MessagesView>();
        using var reader = await cmd.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            var headers = GetValue<Dictionary<string, string>>(reader, "headers");
            var messageMetadata = GetValue<Dictionary<string, object>>(reader, "message_metadata");

            results.Add(new MessagesView
            {
                Id = GetValue<string>(reader, "unique_message_id"),
                MessageId = GetValue<string>(reader, "message_id"),
                MessageType = GetValue<string>(reader, "message_type"),
                SendingEndpoint = DeserializeOrDefault<EndpointDetails>(messageMetadata, "SendingEndpoint"),
                ReceivingEndpoint = DeserializeOrDefault<EndpointDetails>(messageMetadata, "ReceivingEndpoint"),
                TimeSent = GetValue<DateTime>(reader, "time_sent"),
                ProcessedAt = GetValue<DateTime>(reader, "processed_at"),
                CriticalTime = GetValue<TimeSpan>(reader, "critical_time"),
                ProcessingTime = GetValue<TimeSpan>(reader, "processing_time"),
                DeliveryTime = GetValue<TimeSpan>(reader, "delivery_time"),
                IsSystemMessage = GetValue<bool>(reader, "is_system_message"),
                ConversationId = GetValue<string>(reader, "conversation_id"),
                Headers = [.. headers],
                Status = (MessageStatus)GetValue<int>(reader, "status"),
                MessageIntent = (MessageIntent)DeserializeOrDefault(messageMetadata, "MessageIntent", 1),
                BodyUrl = "",
                BodySize = DeserializeOrDefault(messageMetadata, "ContentLength", 0),
                InvokedSagas = DeserializeOrDefault<List<SagaInfo>>(messageMetadata, "InvokedSagas", []),
                OriginatesFromSaga = DeserializeOrDefault<SagaInfo>(messageMetadata, "OriginatesFromSaga")
            });
        }
        return new QueryResult<IList<MessagesView>>(results, new QueryStatsInfo(string.Empty, results.Count));
    }
}
