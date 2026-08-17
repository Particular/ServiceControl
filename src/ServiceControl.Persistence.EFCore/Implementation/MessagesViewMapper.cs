namespace ServiceControl.Persistence.EFCore.Implementation;

using NServiceBus;
using ServiceControl.CompositeViews.Messages;
using ServiceControl.MessageFailures;
using ServiceControl.Persistence.EFCore.Entities;
using ServiceControl.Persistence.EFCore.Infrastructure;

static class MessagesViewMapper
{
    public static MessagesView ToMessagesView(this FailedMessageEntity entity)
    {
        var headers = MessageHeaders.Read(entity.HeadersJson);

        return new MessagesView
        {
            Id = entity.UniqueMessageId.ToString(),
            MessageId = entity.MessageId,
            MessageType = entity.MessageType,
            SendingEndpoint = entity.ToSendingEndpoint(),
            ReceivingEndpoint = entity.ToReceivingEndpoint(),
            TimeSent = entity.TimeSent,
            ProcessedAt = entity.LastAttemptedAt,
            // The error instance never enriches the processing statistics: ProcessingStatisticsEnricher
            // contributes TimeSent and nothing else.
            CriticalTime = TimeSpan.Zero,
            ProcessingTime = TimeSpan.Zero,
            DeliveryTime = TimeSpan.Zero,
            IsSystemMessage = entity.IsSystemMessage,
            ConversationId = entity.ConversationId,
            Headers = [.. headers.Select(header => new KeyValuePair<string, object>(header.Key, header.Value))],
            Status = entity.ToMessageStatus(),
            MessageIntent = ReadMessageIntent(headers),
            BodyUrl = $"/messages/{entity.UniqueMessageId}/body",
            BodySize = entity.BodySize
        };
    }

    public static MessageStatus ToMessageStatus(this FailedMessageEntity entity) =>
        entity.Status switch
        {
            FailedMessageStatus.Resolved => MessageStatus.ResolvedSuccessfully,
            FailedMessageStatus.RetryIssued => MessageStatus.RetryIssued,
            FailedMessageStatus.Archived => MessageStatus.ArchivedFailure,
            FailedMessageStatus.Unresolved or _ => entity.NumberOfProcessingAttempts == 1 ? MessageStatus.Failed : MessageStatus.RepeatedFailure
        };

    static MessageIntent ReadMessageIntent(Dictionary<string, string> headers)
    {
        var intent = default(MessageIntent);

        if (headers.TryGetValue(Headers.MessageIntent, out var value))
        {
            Enum.TryParse(value, true, out intent);
        }

        return intent;
    }
}
