namespace ServiceControl.CompositeViews.Messages
{
    using System;
    using System.Collections;
    using System.Collections.Generic;
    using System.Linq;
    using System.Linq.Expressions;
    using MessageFailures;
    using Raven.Client.Documents.Linq;
    using ServiceControl.Persistence;

    class FakeAbstractTransformerCreationTask<TFrom>
    {
        public Expression<Func<IEnumerable<TFrom>, IEnumerable>> TransformResults;
        public string TransformerName;
    }

    class MessagesViewTransformer : FakeAbstractTransformerCreationTask<MessagesViewTransformer.Input> // https://ravendb.net/docs/article-page/4.2/csharp/migration/client-api/session/querying/transformers
    {
        public MessagesViewTransformer()
        {
            TransformResults = messages => from message in messages
                                           let metadata =
                    message.ProcessingAttempts != null
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
                                           select new
                                           {
                                               Id = message.UniqueMessageId,
                                               MessageId = metadata["MessageId"],
                                               MessageType = metadata["MessageType"],
                                               SendingEndpoint = metadata["SendingEndpoint"],
                                               ReceivingEndpoint = metadata["ReceivingEndpoint"],
                                               TimeSent = (DateTime?)metadata["TimeSent"],
                                               ProcessedAt = processedAt,
                                               CriticalTime = (TimeSpan)metadata["CriticalTime"],
                                               ProcessingTime = (TimeSpan)metadata["ProcessingTime"],
                                               DeliveryTime = (TimeSpan)metadata["DeliveryTime"],
                                               IsSystemMessage = (bool)metadata["IsSystemMessage"],
                                               ConversationId = metadata["ConversationId"],
                                               //the reason the we need to use a KeyValuePair<string, object> is that raven seems to interpret the values and convert them
                                               // to real types. In this case it was the NServiceBus.Temporary.DelayDeliveryWith header to was converted to a timespan
                                               Headers = headers.Select(header => new KeyValuePair<string, object>(header.Key, header.Value)),
                                               Status = status,
                                               MessageIntent = metadata["MessageIntent"],
                                               BodyUrl = metadata["BodyUrl"],
                                               BodySize = (int)metadata["ContentLength"],
                                               InvokedSagas = metadata["InvokedSagas"],
                                               OriginatesFromSaga = metadata["OriginatesFromSaga"]
                                           };
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

