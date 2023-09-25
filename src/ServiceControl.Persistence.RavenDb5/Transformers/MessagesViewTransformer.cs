namespace ServiceControl.CompositeViews.Messages
{
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
                let metadata = message.ProcessingAttempts.Last().MessageMetadata
                let headers = message.ProcessingAttempts.Last().Headers
                let processedAt = message.ProcessingAttempts.Last().AttemptedAt
                let status = message.Status == FailedMessageStatus.Resolved
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
                    message.MessageId,
                    MessageType = metadata["MessageType"],
                    SendingEndpoint = metadata["SendingEndpoint"],
                    ReceivingEndpoint = metadata["ReceivingEndpoint"],
                    TimeSent = metadata["TimeSent"],
                    ProcessedAt = processedAt,
                    CriticalTime = metadata["CriticalTime"] ?? "00:00:00",
                    ProcessingTime = metadata["ProcessingTime"] ?? "00:00:00",
                    DeliveryTime = metadata["DeliveryTime"] ?? "00:00:00",
                    IsSystemMessage = metadata["IsSystemMessage"] ?? false,
                    ConversationId = metadata["ConversationId"],
                    //the reason the we need to use a KeyValuePair<string, object> is that raven seems to interpret the values and convert them
                    // to real types. In this case it was the NServiceBus.Temporary.DelayDeliveryWith header to was converted to a timespan
                    Headers = headers.Select(header => new { header.Key, header.Value }),
                    Status = status,
                    MessageIntent = metadata["MessageIntent"],
                    BodyUrl = metadata["BodyUrl"],
                    BodySize = metadata["ContentLength"] ?? 0,
                    InvokedSagas = metadata["InvokedSagas"],
                    OriginatesFromSaga = metadata["OriginatesFromSaga"]
                };

            return results.OfType<MessagesView>();
        }

        public class Input : MessagesViewIndex.SortAndFilterOptions
        {
            public new FailedMessageStatus Status { get; }
            public string UniqueMessageId { get; }
            public List<FailedMessage.ProcessingAttempt> ProcessingAttempts { get; }
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