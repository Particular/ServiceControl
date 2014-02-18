namespace ServiceControl.CompositeViews.Messages
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using Contracts.Operations;
    using MessageFailures;
    using Raven.Client.Indexes;
    public class MessagesBodyTransformer2 : AbstractTransformerCreationTask<MessagesViewTransformer.Result>
    {
        public class Result
        {
            public string MessageId { get; set; }
            public byte[] Body { get; set; }
            public string ContentType { get; set; }
            public string BodySize { get; set; }
        }

        public MessagesBodyTransformer2()
        {
            TransformResults = messages => from message in messages
                let metadata =
                    message.ProcessingAttempts != null
                        ? message.ProcessingAttempts.Last().MessageMetadata
                        : message.MessageMetadata
                select new
                {
                    MessageId = metadata["MessageId"],
                    Body = metadata["Body"],
                    BodySize = metadata["BodySize"],
                    ContentType = metadata["ContentType"],
                };
        }
    }

    public class MessagesViewTransformer : AbstractTransformerCreationTask<MessagesViewTransformer.Result>
    {
        public class Result
        {
            public string Id { get; set; }
            public string UniqueMessageId { get; set; }
            public DateTime ProcessedAt { get; set; }
            public Dictionary<string, string> Headers { get; set; }
            public Dictionary<string, object> MessageMetadata { get; set; }
            public List<FailedMessage.ProcessingAttempt> ProcessingAttempts { get; set; }
            public FailedMessageStatus Status { get; set; }
        }

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
                        ? LoadDocument<FailedMessage>("FailedMessages/" + message.UniqueMessageId) == null ? MessageStatus.Successful : MessageStatus.ResolvedSuccessfully
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
                    TimeSent = (DateTime) metadata["TimeSent"],
                    ProcessedAt = processedAt,
                    CriticalTime = (TimeSpan) metadata["CriticalTime"],
                    ProcessingTime = (TimeSpan) metadata["ProcessingTime"],
                    DeliveryTime = (TimeSpan) metadata["DeliveryTime"],
                    IsSystemMessage = (bool) metadata["IsSystemMessage"],
                    ConversationId = metadata["ConversationId"],
                    //the reason the we need to use a KeyValuePair<string, object> is that raven seems to interpret the values and convert them
                    // to real types. In this case it was the NServiceBus.Temporary.DelayDeliveryWith header to was converted to a timespan
                    Headers = headers.Select(header => new KeyValuePair<string, object>(header.Key, header.Value)),
                    Status = status,
                    MessageIntent = metadata["MessageIntent"],
                    BodyUrl = metadata["BodyUrl"],
                    BodySize = (int) metadata["BodySize"],
                    InvokedSagas = metadata["InvokedSagas"],
                    OriginatesFromSaga = metadata["OriginatesFromSaga"]
                };
        }
    }
}