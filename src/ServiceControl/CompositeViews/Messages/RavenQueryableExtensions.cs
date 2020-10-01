namespace ServiceControl.CompositeViews.Messages
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using Contracts.Operations;
    using MessageFailures;

    static class RavenQueryableExtensions
    {
        // TODO: RAVEN5 No transformers
        public static IEnumerable<MessagesView> ToMessagesView(this IEnumerable<FailedMessage> query)
            => from message in query
                let attempt = message.ProcessingAttempts.OrderByDescending(x => x.AttemptedAt).First()
                select new MessagesView
                {
                    Id = message.UniqueMessageId,
                    MessageId = attempt.MessageId,
                    SendingEndpoint = (EndpointDetails)attempt.MessageMetadata["SendingEndpoint"],
                    ReceivingEndpoint = (EndpointDetails)attempt.MessageMetadata["ReceivingEndpoint"],
                    Headers = attempt.Headers.Select(x => new KeyValuePair<string, object>(x.Key, x.Value)),
                    ConversationId = (string)attempt.MessageMetadata["ConversationId"],
                    MessageType = (string)attempt.MessageMetadata["MessageType"],
                    IsSystemMessage = (bool)attempt.MessageMetadata["IsSystemMessage"],
                    BodyUrl = (string)attempt.MessageMetadata["BodyUrl"],
                    BodySize = (int)(long)attempt.MessageMetadata["ContentLength"],
                    //MessageIntent = (MessageIntentEnum)attempt.MessageMetadata["MessageIntent"],
                    //InstanceId = (string)attempt.MessageMetadata["InstanceId"],
                    ProcessedAt = attempt.AttemptedAt,
                    //CriticalTime = (TimeSpan)attempt.MessageMetadata["CriticalTime"],
                    //ProcessingTime = (TimeSpan)attempt.MessageMetadata["ProcessingTime"],
                    //DeliveryTime = (TimeSpan)attempt.MessageMetadata["DeliveryTime"],
                    TimeSent = attempt.MessageMetadata.TryGetValue("TimeSent", out var timeSentValue) ? DateTime.SpecifyKind(DateTime.Parse((string)timeSentValue), DateTimeKind.Utc) : default(DateTime?),
                    //InvokedSagas = (List<SagaInfo>)attempt.MessageMetadata["InvokedSagas"],
                    //OriginatesFromSaga = (SagaInfo)attempt.MessageMetadata["OriginatesFromSaga"],
                    Status =  message.Status == FailedMessageStatus.RetryIssued
                        ? MessageStatus.RetryIssued
                        : message.Status == FailedMessageStatus.Archived
                            ? MessageStatus.ArchivedFailure
                            : message.ProcessingAttempts.Count == 1
                                ? MessageStatus.Failed
                                : MessageStatus.RepeatedFailure
                };
    }
}