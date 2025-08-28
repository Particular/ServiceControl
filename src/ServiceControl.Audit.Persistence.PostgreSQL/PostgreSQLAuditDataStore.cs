namespace ServiceControl.Audit.Persistence.PostgreSQL;

using System;
using System.Collections.Generic;
using System.Text;
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

    async Task<QueryResult<IList<MessagesView>>> GetAllMessages(
            string? conversationId, bool? includeSystemMessages, PagingInfo pagingInfo, SortInfo sortInfo, DateTimeRange timeSentRange,
            string? endpointName,
            string? q,
            CancellationToken cancellationToken)
    {
        using var conn = await connectionFactory.OpenConnection(cancellationToken);

        var sql = new StringBuilder(@"select unique_message_id,
                message_metadata,
                headers,
                processed_at,
                message_id,
                message_type,
                is_system_message,
                status,
                time_sent,
                receiving_endpoint_name,
                critical_time,
                processing_time,
                delivery_time,
                conversation_id from processed_messages
            where 1 = 1");

        if (includeSystemMessages.HasValue)
        {
            sql.Append(" and is_system_message = @is_system_message");
        }

        if (!string.IsNullOrWhiteSpace(q))
        {
            sql.Append(" and query @@ to_tsquery('english', @search)");
        }

        if (!string.IsNullOrWhiteSpace(conversationId))
        {
            sql.Append(" and conversation_id = @conversation_id");
        }

        if (!string.IsNullOrWhiteSpace(endpointName))
        {
            sql.Append(" and receiving_endpoint_name = @endpoint_name");
        }

        if (timeSentRange?.From != null)
        {
            sql.Append(" and time_sent >= @time_sent_start");
        }

        if (timeSentRange?.To != null)
        {
            sql.Append(" and time_sent <= @time_sent_end");
        }

        sql.Append(" ORDER BY");
        switch (sortInfo.Sort)
        {

            case "id":
            case "message_id":
                sql.Append(" message_id");
                break;
            case "message_type":
                sql.Append(" message_type");
                break;
            case "critical_time":
                sql.Append(" critical_time");
                break;
            case "delivery_time":
                sql.Append(" delivery_time");
                break;
            case "processing_time":
                sql.Append(" processing_time");
                break;
            case "processed_at":
                sql.Append(" processed_at");
                break;
            case "status":
                sql.Append(" status");
                break;
            default:
                sql.Append(" time_sent");
                break;
        }

        if (sortInfo.Direction == "asc")
        {
            sql.Append(" ASC");
        }
        else
        {
            sql.Append(" DESC");
        }

        sql.Append($" LIMIT {pagingInfo.PageSize} OFFSET {pagingInfo.Offset};");

        var query = sql.ToString();
        using var cmd = new NpgsqlCommand(query, conn);

        if (!string.IsNullOrWhiteSpace(q))
        {
            cmd.Parameters.AddWithValue("search", q);
        }
        if (!string.IsNullOrWhiteSpace(endpointName))
        {
            cmd.Parameters.AddWithValue("endpoint_name", endpointName);
        }
        if (!string.IsNullOrWhiteSpace(conversationId))
        {
            cmd.Parameters.AddWithValue("conversation_id", conversationId);
        }
        if (includeSystemMessages.HasValue)
        {
            cmd.Parameters.AddWithValue("is_system_message", includeSystemMessages);
        }
        if (timeSentRange?.From != null)
        {
            cmd.Parameters.AddWithValue("time_sent_start", timeSentRange.From);
        }
        if (timeSentRange?.To != null)
        {
            cmd.Parameters.AddWithValue("time_sent_end", timeSentRange.To);
        }

        return await ReturnResults(cmd, cancellationToken);
    }

    public async Task<QueryResult<IList<MessagesView>>> GetMessages(bool includeSystemMessages, PagingInfo pagingInfo, SortInfo sortInfo, DateTimeRange timeSentRange, CancellationToken cancellationToken)
    {
        return await GetAllMessages(null, includeSystemMessages, pagingInfo, sortInfo, timeSentRange, null, null, cancellationToken);
    }

    public Task<QueryResult<IList<AuditCount>>> QueryAuditCounts(string endpointName, CancellationToken cancellationToken) => throw new NotImplementedException();
    public Task<QueryResult<IList<KnownEndpointsView>>> QueryKnownEndpoints(CancellationToken cancellationToken) => throw new NotImplementedException();
    public Task<QueryResult<IList<MessagesView>>> QueryMessages(string searchParam, PagingInfo pagingInfo, SortInfo sortInfo, DateTimeRange timeSentRange = null, CancellationToken cancellationToken = default)
    {
        return GetAllMessages(null, null, pagingInfo, sortInfo, timeSentRange, searchParam, null, cancellationToken);
    }
    public async Task<QueryResult<IList<MessagesView>>> QueryMessagesByConversationId(string conversationId, PagingInfo pagingInfo, SortInfo sortInfo, CancellationToken cancellationToken)
    {
        return await GetAllMessages(conversationId, null, pagingInfo, sortInfo, null, null, null, cancellationToken);
    }

    public async Task<QueryResult<IList<MessagesView>>> QueryMessagesByReceivingEndpoint(bool includeSystemMessages, string endpointName, PagingInfo pagingInfo, SortInfo sortInfo, DateTimeRange timeSentRange = null, CancellationToken cancellationToken = default)
    {
        return await GetAllMessages(null, includeSystemMessages, pagingInfo, sortInfo, timeSentRange, null, endpointName, cancellationToken);
    }

    public Task<QueryResult<IList<MessagesView>>> QueryMessagesByReceivingEndpointAndKeyword(string endpoint, string keyword, PagingInfo pagingInfo, SortInfo sortInfo, DateTimeRange timeSentRange = null, CancellationToken cancellationToken = default)
    {
        return GetAllMessages(null, null, pagingInfo, sortInfo, timeSentRange, keyword, endpoint, cancellationToken);
    }
    public Task<QueryResult<SagaHistory>> QuerySagaHistoryById(Guid input, CancellationToken cancellationToken) => throw new NotImplementedException();

    async Task<QueryResult<IList<MessagesView>>> ReturnResults(NpgsqlCommand cmd, CancellationToken cancellationToken = default)
    {
        var results = new List<MessagesView>();

        using var reader = await cmd.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            var headers = reader.GetFieldValue<Dictionary<string, string>>(reader.GetOrdinal("headers"));
            var messageMetadata = reader.GetFieldValue<Dictionary<string, object>>(reader.GetOrdinal("message_metadata"));

            results.Add(new MessagesView
            {
                Id = reader.GetFieldValue<string>(reader.GetOrdinal("unique_message_id")),
                MessageId = reader.GetFieldValue<string>(reader.GetOrdinal("message_id")),
                MessageType = reader.GetFieldValue<string>(reader.GetOrdinal("message_type")),
                SendingEndpoint = JsonSerializer.Deserialize<EndpointDetails>((JsonElement)messageMetadata["SendingEndpoint"]),
                ReceivingEndpoint = JsonSerializer.Deserialize<EndpointDetails>((JsonElement)messageMetadata["ReceivingEndpoint"]),
                TimeSent = reader.GetFieldValue<DateTime>(reader.GetOrdinal("time_sent")),
                ProcessedAt = reader.GetFieldValue<DateTime>(reader.GetOrdinal("processed_at")),
                CriticalTime = reader.GetFieldValue<TimeSpan>(reader.GetOrdinal("critical_time")),
                ProcessingTime = reader.GetFieldValue<TimeSpan>(reader.GetOrdinal("processing_time")),
                DeliveryTime = reader.GetFieldValue<TimeSpan>(reader.GetOrdinal("delivery_time")),
                IsSystemMessage = reader.GetFieldValue<bool>(reader.GetOrdinal("is_system_message")),
                ConversationId = reader.GetFieldValue<string>(reader.GetOrdinal("conversation_id")),
                Headers = [.. headers],
                Status = (MessageStatus)reader.GetFieldValue<int>(reader.GetOrdinal("status")),
                MessageIntent = (MessageIntent)(messageMetadata.ContainsKey("MessageIntent") ? JsonSerializer.Deserialize<int>((JsonElement)messageMetadata["MessageIntent"]) : 1),
                BodyUrl = "",
                BodySize = messageMetadata.ContainsKey("ContentLength") ? JsonSerializer.Deserialize<int>((JsonElement)messageMetadata["ContentLength"]) : 0,
                InvokedSagas = messageMetadata.ContainsKey("InvokedSagas") ? JsonSerializer.Deserialize<List<SagaInfo>>((JsonElement)messageMetadata["InvokedSagas"]) : [],
                OriginatesFromSaga = messageMetadata.ContainsKey("OriginatesFromSaga") ? JsonSerializer.Deserialize<SagaInfo>((JsonElement)messageMetadata["OriginatesFromSaga"]) : null
            });
        }

        return new QueryResult<IList<MessagesView>>(results, new QueryStatsInfo(string.Empty, results.Count));
    }
}
