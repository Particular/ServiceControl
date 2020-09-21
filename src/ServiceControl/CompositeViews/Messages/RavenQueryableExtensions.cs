namespace ServiceControl.CompositeViews.Messages
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using Contracts.Operations;
    using MessageFailures;
    using NServiceBus;
    using SagaAudit;

    static class RavenQueryableExtensions
    {
        public static IEnumerable<MessagesView> ToMessagesView(this IEnumerable<FailedMessage> query)
            => from message in query
                let attempt = message.ProcessingAttempts.OrderByDescending(x => x.AttemptedAt).First()
                select new MessagesView
                {
                    Id = message.UniqueMessageId,
                    MessageId = attempt.MessageId,
                    SendingEndpoint = attempt.Meta<EndpointDetails>("SendingEndpoint"),
                    ReceivingEndpoint = attempt.Meta<EndpointDetails>("ReceivingEndpoint"),
                    Headers = attempt.Headers.Select(x => new KeyValuePair<string, object>(x.Key, x.Value)),
                    ConversationId = attempt.Meta<string>("ConversationId"),
                    MessageType = attempt.Meta<string>("MessageType"),
                    IsSystemMessage = attempt.Meta<bool>("IsSystemMessage"),
                    BodyUrl = attempt.Meta<string>("BodyUrl"),
                    BodySize = (int)attempt.Meta<long>("ContentLength"),
                    MessageIntent = (MessageIntentEnum)Enum.Parse(typeof(MessageIntentEnum), attempt.Meta<string>("MessageIntent")),
                    InstanceId = attempt.Meta<string>("InstanceId"),
                    ProcessedAt = attempt.AttemptedAt,
                    CriticalTime = attempt.Meta<TimeSpan>("CriticalTime"),
                    ProcessingTime = attempt.Meta<TimeSpan>("ProcessingTime"),
                    DeliveryTime = attempt.Meta<TimeSpan>("DeliveryTime"),
                    TimeSent = attempt.MessageMetadata.TryGetValue("TimeSent", out var timeSentValue) ? DateTime.SpecifyKind(DateTime.Parse((string)timeSentValue), DateTimeKind.Utc) : default(DateTime?),
                    InvokedSagas = attempt.Meta<List<SagaInfo>>("InvokedSagas"),
                    OriginatesFromSaga = attempt.Meta<SagaInfo>("OriginatesFromSaga"),
                    Status =  message.Status == FailedMessageStatus.RetryIssued
                        ? MessageStatus.RetryIssued
                        : message.Status == FailedMessageStatus.Archived
                            ? MessageStatus.ArchivedFailure
                            : message.ProcessingAttempts.Count == 1
                                ? MessageStatus.Failed
                                : MessageStatus.RepeatedFailure
                };

        static T Meta<T>(this FailedMessage.ProcessingAttempt attempt, string key, T defaultValue = default)
        {
            if (attempt.MessageMetadata.TryGetValue(key, out var value) && value != null)
            {
                if (value is T convertedValue)
                {
                    return convertedValue;
                }

                throw new Exception($"{key} is not {typeof(T).Name}");
            }

            return defaultValue;
        }
    }
}