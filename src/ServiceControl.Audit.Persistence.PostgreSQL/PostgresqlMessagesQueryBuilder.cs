


namespace ServiceControl.Audit.Persistence.PostgreSQL;

using System;
using System.Collections.Generic;
using System.Text;
using Npgsql;
using ServiceControl.Audit.Infrastructure;
public class PostgresqlMessagesQueryBuilder
{
    readonly StringBuilder sql = new();
    readonly List<NpgsqlParameter> parameters = [];

    public PostgresqlMessagesQueryBuilder()
    {
        sql.Append(@"select unique_message_id,
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
    }

    public PostgresqlMessagesQueryBuilder WithSystemMessages(bool? includeSystemMessages)
    {
        if (includeSystemMessages.HasValue)
        {
            sql.Append(" and is_system_message = @is_system_message");
            parameters.Add(new NpgsqlParameter("is_system_message", includeSystemMessages));
        }
        return this;
    }

    public PostgresqlMessagesQueryBuilder WithSearch(string? q)
    {
        if (!string.IsNullOrWhiteSpace(q))
        {
            sql.Append(" and query @@ to_tsquery('english', @search)");
            parameters.Add(new NpgsqlParameter("search", q));
        }
        return this;
    }

    public PostgresqlMessagesQueryBuilder WithConversationId(string? conversationId)
    {
        if (!string.IsNullOrWhiteSpace(conversationId))
        {
            sql.Append(" and conversation_id = @conversation_id");
            parameters.Add(new NpgsqlParameter("conversation_id", conversationId));
        }
        return this;
    }

    public PostgresqlMessagesQueryBuilder WithMessageId(string? messageId)
    {
        if (!string.IsNullOrWhiteSpace(messageId))
        {
            sql.Append(" and message_id = @message_id");
            parameters.Add(new NpgsqlParameter("message_id", messageId));
        }
        return this;
    }

    public PostgresqlMessagesQueryBuilder WithEndpointName(string? endpointName)
    {
        if (!string.IsNullOrWhiteSpace(endpointName))
        {
            sql.Append(" and receiving_endpoint_name = @endpoint_name");
            parameters.Add(new NpgsqlParameter("endpoint_name", endpointName));
        }
        return this;
    }

    public PostgresqlMessagesQueryBuilder WithTimeSentRange(DateTimeRange? timeSentRange)
    {
        if (timeSentRange?.From != null)
        {
            sql.Append(" and time_sent >= @time_sent_start");
            parameters.Add(new NpgsqlParameter("time_sent_start", timeSentRange.From));
        }
        if (timeSentRange?.To != null)
        {
            sql.Append(" and time_sent <= @time_sent_end");
            parameters.Add(new NpgsqlParameter("time_sent_end", timeSentRange.To));
        }
        return this;
    }

    public PostgresqlMessagesQueryBuilder WithSorting(SortInfo sortInfo)
    {
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
        sql.Append(sortInfo.Direction == "asc" ? " ASC" : " DESC");
        return this;
    }

    public PostgresqlMessagesQueryBuilder WithPaging(PagingInfo pagingInfo)
    {
        sql.Append($" LIMIT {pagingInfo.PageSize} OFFSET {pagingInfo.Offset};");
        return this;
    }

    public (string Sql, List<NpgsqlParameter> Parameters) Build()
    {
        return (sql.ToString(), parameters);
    }
}
