namespace ServiceControl.CompositeViews.Messages
{
    using System.Linq;
    using MessageFailures;
    using Raven.Client.Documents.Linq;
    using ServiceControl.Persistence;

    static class MessagesViewTransformer //https://ravendb.net/docs/article-page/4.2/csharp/migration/client-api/session/querying/transformers
    {
        public static IQueryable<MessagesView> TransformToMessageView(this IQueryable<FailedMessage> query)
        {
            var results =
                from message in query
                let status = message.Status == FailedMessageStatus.Resolved
                    ? MessageStatus.ResolvedSuccessfully
                    : message.Status == FailedMessageStatus.RetryIssued
                        ? MessageStatus.RetryIssued
                        : message.Status == FailedMessageStatus.Archived
                            ? MessageStatus.ArchivedFailure
                            : message.ProcessingAttempts.Count == 1
                                ? MessageStatus.Failed
                                : MessageStatus.RepeatedFailure
                let last = message.ProcessingAttempts.Last()
                let metadata = last.MessageMetadata
                let headers = last.Headers
                let processedAt = last.AttemptedAt

                select new // Cannot use type here as this is projected server-side
                {
                    Id = message.UniqueMessageId,
                    MessageId = metadata["MessageId"],
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
    }
}