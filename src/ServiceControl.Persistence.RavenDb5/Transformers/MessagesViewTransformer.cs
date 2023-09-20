namespace ServiceControl.CompositeViews.Messages
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using MessageFailures;
    using Raven.Client.Documents.Linq;
    using ServiceControl.Persistence;

    class MessagesViewTransformer //https://ravendb.net/docs/article-page/4.2/csharp/migration/client-api/session/querying/transformers
    {
        public static IQueryable<MessagesView> Transform(IRavenQueryable<Input> query)
        {
            var results =
                from message in query
                let metadata = message.ProcessingAttempts != null
                    ? message.ProcessingAttempts.Last().MessageMetadata
                    : message.MessageMetadata
                let headers =
                    message.ProcessingAttempts != null ? message.ProcessingAttempts.Last().Headers : message.Headers
                let processedAt =
                    message.ProcessingAttempts != null
                        ? message.ProcessingAttempts.Last().AttemptedAt
                        : message.ProcessedAt
                let status =
                    message.ProcessingAttempts == null
                        ? !(bool)message.MessageMetadata["IsRetried"]
                            ? MessageStatus.Successful
                            : MessageStatus.ResolvedSuccessfully
                        : message.Status == FailedMessageStatus.Resolved
                            ? MessageStatus.ResolvedSuccessfully
                            : message.Status == FailedMessageStatus.RetryIssued
                                ? MessageStatus.RetryIssued
                                : message.Status == FailedMessageStatus.Archived
                                    ? MessageStatus.ArchivedFailure
                                    : message.ProcessingAttempts.Count == 1
                                        ? MessageStatus.Failed
                                        : MessageStatus.RepeatedFailure
                select new // Cannot use type here as this is projected server-side
                {
                    Id = message.UniqueMessageId,
                    MessageId = metadata["MessageId"],
                    MessageType = metadata["MessageType"],
                    SendingEndpoint = metadata["SendingEndpoint"],
                    ReceivingEndpoint = metadata["ReceivingEndpoint"],
                    TimeSent = metadata["TimeSent"],
                    ProcessedAt = processedAt,
                    CriticalTime = metadata["CriticalTime"],
                    ProcessingTime = metadata["ProcessingTime"],
                    DeliveryTime = metadata["DeliveryTime"],
                    IsSystemMessage = metadata["IsSystemMessage"],
                    ConversationId = metadata["ConversationId"],
                    //the reason the we need to use a KeyValuePair<string, object> is that raven seems to interpret the values and convert them
                    // to real types. In this case it was the NServiceBus.Temporary.DelayDeliveryWith header to was converted to a timespan
                    Headers = headers.Select(header => new { header.Key, header.Value }),
                    Status = status,
                    MessageIntent = metadata["MessageIntent"],
                    BodyUrl = metadata["BodyUrl"],
                    BodySize = metadata["ContentLength"],
                    InvokedSagas = metadata["InvokedSagas"],
                    OriginatesFromSaga = metadata["OriginatesFromSaga"]
                };

            return results.OfType<MessagesView>();
        }

        public class Input
        {
            public string Id { get; set; }
            public string UniqueMessageId { get; set; }
            public DateTime ProcessedAt { get; set; }
            public Dictionary<string, string> Headers { get; set; }
            public Dictionary<string, object> MessageMetadata { get; set; }
            public List<FailedMessage.ProcessingAttempt> ProcessingAttempts { get; set; }
            public FailedMessageStatus Status { get; set; }
        }
    }

    public static class MessageViewTransformerExtension
    {
        public static IQueryable<MessagesView> TransformToMessagesView(this IQueryable<MessagesViewIndex.SortAndFilterOptions> source)
        {
            return source.Select(m => new MessagesView());
        }
    }
}