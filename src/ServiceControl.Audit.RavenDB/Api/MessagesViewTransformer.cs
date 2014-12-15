namespace ServiceControl.CompositeViews.Messages
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using Contracts.Operations;
    using Raven.Client.Indexes;
    using ServiceControl.MessageAuditing;

    public class MessagesViewTransformer : AbstractTransformerCreationTask<MessagesViewTransformer.Result>
    {
        public class Result
        {
            public string Id { get; set; }
            public string UniqueMessageId { get; set; }
            public DateTime ProcessedAt { get; set; }
            public Dictionary<string, string> Headers { get; set; }
            public Dictionary<string, object> MessageMetadata { get; set; }
            public FailedMessage.ProcessingAttempt LastProcessingAttempt { get; set; }
            public MessageStatus Status { get; set; }
        }

        public MessagesViewTransformer()
        {
            TransformResults = messages => from message in messages
                                           let metadata =
                                               message.LastProcessingAttempt != null
                                                   ? message.LastProcessingAttempt.MessageMetadata
                                                   : message.MessageMetadata

                                           let headers =
                                               message.LastProcessingAttempt != null
                                               ? message.LastProcessingAttempt.Headers
                                               : message.Headers

                                           let processedAt =
                                               message.LastProcessingAttempt != null
                                                   ? message.LastProcessingAttempt.AttemptedAt
                                                   : message.ProcessedAt

                                           select new
                                           {
                                               Id = message.UniqueMessageId,
                                               MessageId = metadata["MessageId"],
                                               MessageType = metadata["MessageType"],
                                               SendingEndpoint = metadata["SendingEndpoint"],
                                               ReceivingEndpoint = metadata["ReceivingEndpoint"],
                                               TimeSent = (DateTime)metadata["TimeSent"],
                                               ProcessedAt = processedAt,
                                               CriticalTime = (TimeSpan)metadata["CriticalTime"],
                                               ProcessingTime = (TimeSpan)metadata["ProcessingTime"],
                                               DeliveryTime = (TimeSpan)metadata["DeliveryTime"],
                                               IsSystemMessage = (bool)metadata["IsSystemMessage"],
                                               ConversationId = metadata["ConversationId"],
                                               //the reason the we need to use a KeyValuePair<string, object> is that raven seems to interpret the values and convert them
                                               // to real types. In this case it was the NServiceBus.Temporary.DelayDeliveryWith header to was converted to a timespan
                                               Headers = headers.Select(header => new KeyValuePair<string, object>(header.Key, header.Value)),
                                               Status = message.Status,
                                               MessageIntent = metadata["MessageIntent"],
                                               BodyUrl = metadata["BodyUrl"],
                                               BodySize = (int)metadata["ContentLength"],
                                               InvokedSagas = metadata["InvokedSagas"],
                                               OriginatesFromSaga = metadata["OriginatesFromSaga"]
                                           };
        }
    }
}