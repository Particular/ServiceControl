namespace ServiceControl.Audit.Persistence.EFCore.Implementation;

using System.Linq.Expressions;
using NServiceBus;
using ServiceControl.Audit.Auditing.MessagesView;
using ServiceControl.Audit.Infrastructure;
using ServiceControl.Audit.Monitoring;
using ServiceControl.Audit.Persistence.EFCore.Entities;
using ServiceControl.Audit.Persistence.EFCore.Infrastructure;
using ServiceControl.SagaAudit;

static class MessageQueries
{
    public static IQueryable<AuditMessageEntity> IncludeSystemMessagesWhere(this IQueryable<AuditMessageEntity> source, bool includeSystemMessages) =>
        includeSystemMessages ? source : source.Where(message => !message.IsSystemMessage);

    public static IQueryable<AuditMessageEntity> FilterBySentTimeRange(this IQueryable<AuditMessageEntity> source, DateTimeRange? timeSentRange)
    {
        if (timeSentRange?.From is { } from)
        {
            var fromUtc = AsUtc(from);
            source = source.Where(message => message.TimeSent >= fromUtc);
        }

        if (timeSentRange?.To is { } to)
        {
            var toUtc = AsUtc(to);
            source = source.Where(message => message.TimeSent <= toUtc);
        }

        return source;
    }

    static DateTime AsUtc(DateTime value) => value.Kind switch
    {
        DateTimeKind.Utc => value,
        DateTimeKind.Local => value.ToUniversalTime(),
        DateTimeKind.Unspecified => DateTime.SpecifyKind(value, DateTimeKind.Utc),
        _ => throw new ArgumentOutOfRangeException(nameof(value), value.Kind, "Unknown DateTimeKind")
    };

    public static IOrderedQueryable<AuditMessageEntity> Sort(this IQueryable<AuditMessageEntity> source, SortInfo? sortInfo)
    {
        var descending = sortInfo?.Direction != "asc";

        return sortInfo?.Sort switch
        {
            "id" or "message_id" => source.OrderBy(message => message.MessageId, descending),
            "message_type" => source.OrderBy(message => message.MessageType, descending),
            "critical_time" => source.OrderBy(message => message.CriticalTimeTicks, descending),
            "delivery_time" => source.OrderBy(message => message.DeliveryTimeTicks, descending),
            "processing_time" => source.OrderBy(message => message.ProcessingTimeTicks, descending),
            "processed_at" => source.OrderBy(message => message.ProcessedAt, descending),
            "status" => source.OrderBy(message => message.Status, descending),
            _ => source.OrderBy(message => message.TimeSent, descending)
        };
    }

    public static IQueryable<MessageRow> ToMessageRows(this IQueryable<AuditMessageEntity> source) =>
        source.Select(message => new MessageRow
        {
            CreatedOn = message.CreatedOn,
            Id = message.Id,
            UniqueMessageId = message.UniqueMessageId,
            MessageId = message.MessageId,
            MessageType = message.MessageType,
            TimeSent = message.TimeSent,
            ProcessedAt = message.ProcessedAt,
            ConversationId = message.ConversationId,
            IsSystemMessage = message.IsSystemMessage,
            Status = message.Status,
            SendingEndpointName = message.SendingEndpointName,
            SendingEndpointHostId = message.SendingEndpointHostId,
            SendingEndpointHost = message.SendingEndpointHost,
            ReceivingEndpointName = message.ReceivingEndpointName,
            ReceivingEndpointHostId = message.ReceivingEndpointHostId,
            ReceivingEndpointHost = message.ReceivingEndpointHost,
            CriticalTimeTicks = message.CriticalTimeTicks,
            ProcessingTimeTicks = message.ProcessingTimeTicks,
            DeliveryTimeTicks = message.DeliveryTimeTicks,
            HeadersJson = message.HeadersJson,
            BodyState = message.BodyState,
            BodySize = message.BodySize
        });

    public static MessagesView ToMessagesView(this MessageRow row)
    {
        var headers = MessageHeaders.Read(row.HeadersJson);

        var sagas = new Dictionary<string, object>();
        InvokedSagasParser.Parse(headers, sagas);

        return new MessagesView
        {
            Id = row.UniqueMessageId.ToString(),
            MessageId = row.MessageId,
            MessageType = row.MessageType,
            SendingEndpoint = Endpoint(row.SendingEndpointName, row.SendingEndpointHostId, row.SendingEndpointHost),
            ReceivingEndpoint = Endpoint(row.ReceivingEndpointName, row.ReceivingEndpointHostId, row.ReceivingEndpointHost),
            TimeSent = row.TimeSent,
            ProcessedAt = row.ProcessedAt,
            CriticalTime = TimeSpan.FromTicks(row.CriticalTimeTicks ?? 0),
            ProcessingTime = TimeSpan.FromTicks(row.ProcessingTimeTicks ?? 0),
            DeliveryTime = TimeSpan.FromTicks(row.DeliveryTimeTicks ?? 0),
            IsSystemMessage = row.IsSystemMessage,
            ConversationId = row.ConversationId,
            Headers = [.. headers],
            Status = row.Status,
            MessageIntent = ReadMessageIntent(headers),
            BodyUrl = row.BodyState == BodyState.None ? null : $"/messages/{AuditBodyId.Format(row.CreatedOn, row.UniqueMessageId)}/body",
            BodySize = row.BodySize,
            InvokedSagas = sagas.TryGetValue("InvokedSagas", out var invoked) ? invoked as List<SagaInfo> : null,
            OriginatesFromSaga = sagas.TryGetValue("OriginatesFromSaga", out var originates) ? originates as SagaInfo : null
        };
    }

    static EndpointDetails? Endpoint(string? name, Guid? hostId, string? host) =>
        name is null && hostId is null && host is null
            ? null
            : new EndpointDetails { Name = name, HostId = hostId ?? Guid.Empty, Host = host };

    static MessageIntent ReadMessageIntent(Dictionary<string, string> headers)
    {
        var intent = default(MessageIntent);
        if (headers.TryGetValue(Headers.MessageIntent, out var value))
        {
            Enum.TryParse(value, true, out intent);
        }

        return intent;
    }

    static IOrderedQueryable<AuditMessageEntity> OrderBy<TKey>(this IQueryable<AuditMessageEntity> source, Expression<Func<AuditMessageEntity, TKey>> keySelector, bool descending) =>
        descending
            ? source.OrderByDescending(keySelector).ThenByDescending(message => message.CreatedOn).ThenByDescending(message => message.Id)
            : source.OrderBy(keySelector).ThenBy(message => message.CreatedOn).ThenBy(message => message.Id);
}

sealed class MessageRow
{
    public DateTime CreatedOn { get; init; }
    public long Id { get; init; }
    public Guid UniqueMessageId { get; init; }
    public string? MessageId { get; init; }
    public string? MessageType { get; init; }
    public DateTime? TimeSent { get; init; }
    public DateTime ProcessedAt { get; init; }
    public string? ConversationId { get; init; }
    public bool IsSystemMessage { get; init; }
    public MessageStatus Status { get; init; }
    public string? SendingEndpointName { get; init; }
    public Guid? SendingEndpointHostId { get; init; }
    public string? SendingEndpointHost { get; init; }
    public string? ReceivingEndpointName { get; init; }
    public Guid? ReceivingEndpointHostId { get; init; }
    public string? ReceivingEndpointHost { get; init; }
    public long? CriticalTimeTicks { get; init; }
    public long? ProcessingTimeTicks { get; init; }
    public long? DeliveryTimeTicks { get; init; }
    public string HeadersJson { get; init; } = string.Empty;
    public BodyState BodyState { get; init; }
    public int BodySize { get; init; }
}
