namespace ServiceControl.Persistence.EFCore.Implementation.Audit;

using System.Linq.Expressions;
using NServiceBus;
using ServiceControl.CompositeViews.Messages;
using ServiceControl.MessageFailures;
using ServiceControl.Operations;
using ServiceControl.Persistence.EFCore.DbContexts;
using ServiceControl.Persistence.EFCore.Entities;
using ServiceControl.Persistence.EFCore.Infrastructure;
using ServiceControl.Persistence.Infrastructure;
using ServiceControl.SagaAudit;

static class MessageRowQueries
{
    public static IQueryable<MessageRow> ToRows(this IQueryable<FailedMessageEntity> failed) =>
        failed.Select(message => new MessageRow
        {
            UniqueMessageId = message.UniqueMessageId,
            IsAudit = false,
            MessageId = message.MessageId,
            MessageType = message.MessageType,
            TimeSent = message.TimeSent,
            ProcessedAt = message.LastAttemptedAt,
            ConversationId = message.ConversationId,
            IsSystemMessage = message.IsSystemMessage,
            // The status the view reports, resolved in SQL so that a sort by status orders failed
            // and audited rows alike. A switch expression cannot appear in an expression tree.
            Status = message.Status == FailedMessageStatus.Resolved
                ? MessageStatus.ResolvedSuccessfully
                : message.Status == FailedMessageStatus.RetryIssued
                    ? MessageStatus.RetryIssued
                    : message.Status == FailedMessageStatus.Archived
                        ? MessageStatus.ArchivedFailure
                        : message.NumberOfProcessingAttempts == 1
                            ? MessageStatus.Failed
                            : MessageStatus.RepeatedFailure,
            SendingEndpointName = message.SendingEndpointName,
            SendingEndpointHostId = message.SendingEndpointHostId,
            SendingEndpointHost = message.SendingEndpointHost,
            ReceivingEndpointName = message.ReceivingEndpointName,
            ReceivingEndpointHostId = message.ReceivingEndpointHostId,
            ReceivingEndpointHost = message.ReceivingEndpointHost,
            CriticalTimeTicks = 0L,
            ProcessingTimeTicks = 0L,
            DeliveryTimeTicks = 0L,
            HeadersJson = message.HeadersJson,
            BodySize = message.BodySize,
            Version = message.LastModified,
            Revision = message.NumberOfProcessingAttempts
        });

    /// <summary>
    /// The audit rows that are not shadowed by a failed message. A message that both failed and was
    /// audited shows as failed, whatever the failed row's status, so the anti-join is on the id alone.
    /// </summary>
    public static IQueryable<MessageRow> ToRows(this IQueryable<AuditMessageEntity> audit, ServiceControlDbContext dbContext) =>
        audit
            .Where(message => !dbContext.FailedMessages.Any(failed => failed.UniqueMessageId == message.UniqueMessageId))
            .Select(message => new MessageRow
            {
                UniqueMessageId = message.UniqueMessageId,
                IsAudit = true,
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
                CriticalTimeTicks = message.CriticalTimeTicks ?? 0L,
                ProcessingTimeTicks = message.ProcessingTimeTicks ?? 0L,
                DeliveryTimeTicks = message.DeliveryTimeTicks ?? 0L,
                HeadersJson = message.HeadersJson,
                BodySize = message.BodySize,
                Version = message.CreatedOn,
                Revision = message.Id
            });

    /// <summary>
    /// The sort options of the message endpoints, applied to the common row so that a branch and
    /// the union over both branches order identically. The processing statistics are zero for
    /// every failed message, so those sorts fall through to time sent among equals, which is the
    /// order the failed messages had before there was an audit branch to compare against.
    /// </summary>
    public static IOrderedQueryable<MessageRow> Sort(this IQueryable<MessageRow> rows, SortInfo? sortInfo)
    {
        var descending = sortInfo?.Direction != "asc";

        return sortInfo?.Sort switch
        {
            "id" or "message_id" => rows.OrderBy(row => row.MessageId, descending),
            "message_type" => rows.OrderBy(row => row.MessageType, descending),
            "processed_at" => rows.OrderBy(row => row.ProcessedAt, descending),
            "status" => rows.OrderBy(row => row.Status, descending),
            "critical_time" => rows.OrderByThenTimeSent(row => row.CriticalTimeTicks, descending),
            "delivery_time" => rows.OrderByThenTimeSent(row => row.DeliveryTimeTicks, descending),
            "processing_time" => rows.OrderByThenTimeSent(row => row.ProcessingTimeTicks, descending),
            _ => rows.OrderBy(row => row.TimeSent, descending)
        };
    }

    public static MessagesView ToMessagesView(this MessageRow row)
    {
        var headers = MessageHeaders.Read(row.HeadersJson);
        (List<SagaInfo>? InvokedSagas, SagaInfo? OriginatesFromSaga)? sagas = row.IsAudit ? ParseSagas(headers) : null;

        return new MessagesView
        {
            Id = row.UniqueMessageId.ToString(),
            MessageId = row.MessageId,
            MessageType = row.MessageType,
            SendingEndpoint = Endpoint(row.SendingEndpointName, row.SendingEndpointHostId, row.SendingEndpointHost),
            ReceivingEndpoint = Endpoint(row.ReceivingEndpointName, row.ReceivingEndpointHostId, row.ReceivingEndpointHost),
            TimeSent = row.TimeSent,
            ProcessedAt = row.ProcessedAt,
            CriticalTime = TimeSpan.FromTicks(row.CriticalTimeTicks),
            ProcessingTime = TimeSpan.FromTicks(row.ProcessingTimeTicks),
            DeliveryTime = TimeSpan.FromTicks(row.DeliveryTimeTicks),
            IsSystemMessage = row.IsSystemMessage,
            ConversationId = row.ConversationId,
            Headers = [.. headers.Select(header => new KeyValuePair<string, object>(header.Key, header.Value))],
            Status = row.Status,
            MessageIntent = ReadMessageIntent(headers),
            BodyUrl = $"/messages/{row.UniqueMessageId}/body",
            BodySize = row.BodySize,
            InvokedSagas = sagas?.InvokedSagas,
            OriginatesFromSaga = sagas?.OriginatesFromSaga
        };
    }

    // The saga relationships are a pure function of the headers, which is why they have no columns.
    static (List<SagaInfo>? InvokedSagas, SagaInfo? OriginatesFromSaga) ParseSagas(Dictionary<string, string> headers)
    {
        var metadata = new Dictionary<string, object>();

        InvokedSagasParser.Parse(headers, metadata);

        return (
            metadata.TryGetValue("InvokedSagas", out var invoked) ? invoked as List<SagaInfo> : null,
            metadata.TryGetValue("OriginatesFromSaga", out var originates) ? originates as SagaInfo : null);
    }

    static EndpointDetails? Endpoint(string? name, Guid? hostId, string? host) =>
        name is null && hostId is null && host is null
            ? null
            : new EndpointDetails { Name = name ?? string.Empty, HostId = hostId ?? Guid.Empty, Host = host ?? string.Empty };

    static MessageIntent ReadMessageIntent(Dictionary<string, string> headers)
    {
        var intent = default(MessageIntent);

        if (headers.TryGetValue(Headers.MessageIntent, out var value))
        {
            Enum.TryParse(value, true, out intent);
        }

        return intent;
    }

    static IOrderedQueryable<MessageRow> OrderBy<TKey>(this IQueryable<MessageRow> rows, Expression<Func<MessageRow, TKey>> keySelector, bool descending) =>
        descending
            ? rows.OrderByDescending(keySelector).ThenByDescending(row => row.UniqueMessageId)
            : rows.OrderBy(keySelector).ThenBy(row => row.UniqueMessageId);

    static IOrderedQueryable<MessageRow> OrderByThenTimeSent(this IQueryable<MessageRow> rows, Expression<Func<MessageRow, long>> keySelector, bool descending) =>
        descending
            ? rows.OrderByDescending(keySelector).ThenByDescending(row => row.TimeSent).ThenByDescending(row => row.UniqueMessageId)
            : rows.OrderBy(keySelector).ThenBy(row => row.TimeSent).ThenBy(row => row.UniqueMessageId);
}
