namespace ServiceControl.CompositeViews.Messages
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using Contracts.Operations;
    using Lucene.Net.Analysis.Standard;
    using MessageAuditing;
    using MessageFailures;
    using Raven.Abstractions.Indexing;
    using Raven.Client.Indexes;

    public class MessagesViewIndex : AbstractMultiMapIndexCreationTask<MessagesViewIndex.SortAndFilterOptions>
    {
        public class SortAndFilterOptions
        {
            public string MessageId { get; set; }
            public DateTime ProcessedAt { get; set; }
            public DateTime TimeSent { get; set; }
            public bool IsSystemMessage { get; set; }
            public string MessageType { get; set; }
            public TimeSpan CriticalTime { get; set; }
            public TimeSpan ProcessingTime { get; set; }
            public MessageStatus Status { get; set; }
            public string ReceivingEndpointName { get; set; }
            public string ConversationId { get; set; }
            public string[] Query { get; set; }
        }

        public class Result
        {
            public string UniqueMessageId { get; set; }
            public DateTime ProcessedAt { get; set; }
            public Dictionary<string, string> Headers { get; set; }
            public Dictionary<string, object> MessageMetadata { get; set; }
            public List<FailedMessage.ProcessingAttempt> ProcessingAttempts { get; set; }
        }

        public MessagesViewIndex()
        {
            AddMap<ProcessedMessage>(messages => messages.Select(message => new
            {
                MessageId = message.MessageMetadata["MessageId"],
                ConversationId = message.MessageMetadata["ConversationId"],
                MessageType = message.MessageMetadata["MessageType"],
                MessageIntent = message.MessageMetadata["MessageIntent"],
                IsSystemMessage = message.MessageMetadata["IsSystemMessage"],
                Status = MessageStatus.Successful,
                TimeSent = (DateTime)message.MessageMetadata["TimeSent"],
                message.ProcessedAt,
                ReceivingEndpointName = ((EndpointDetails)message.MessageMetadata["ReceivingEndpoint"]).Name,
                CriticalTime = message.MessageMetadata["CriticalTime"],
                ProcessingTime = message.MessageMetadata["ProcessingTime"],
                Query = message.MessageMetadata.Select(kvp => kvp.Value.ToString())
            }));


            AddMap<FailedMessage>(messages => messages.Select(message => new
            {
                MessageId = message.ProcessingAttempts.Last().MessageMetadata["MessageId"],
                ConversationId = message.ProcessingAttempts.Last().MessageMetadata["ConversationId"],
                MessageType = message.ProcessingAttempts.Last().MessageMetadata["MessageType"],
                MessageIntent = message.ProcessingAttempts.Last().MessageMetadata["MessageIntent"],
                IsSystemMessage = message.ProcessingAttempts.Last().MessageMetadata["IsSystemMessage"],
                Status = message.ProcessingAttempts.Count() == 1 ? MessageStatus.Failed : MessageStatus.RepeatedFailure,
                TimeSent = (DateTime)message.ProcessingAttempts.Last().MessageMetadata["TimeSent"],
                ProcessedAt = message.ProcessingAttempts.Last().FailureDetails.TimeOfFailure,
                ReceivingEndpointName = ((EndpointDetails)message.ProcessingAttempts.Last().MessageMetadata["ReceivingEndpoint"]).Name,
                CriticalTime = TimeSpan.Zero,
                ProcessingTime = TimeSpan.Zero,
                Query = message.ProcessingAttempts.Last().MessageMetadata.Select(kvp => kvp.Value.ToString())
            }));

            Index(x => x.Query, FieldIndexing.Analyzed);
            Index(x => x.CriticalTime, FieldIndexing.Default);
            Index(x => x.ProcessingTime, FieldIndexing.Default);
            Index(x => x.ProcessedAt, FieldIndexing.Default);

            Sort(x => x.CriticalTime, SortOptions.Long);
            Sort(x => x.ProcessingTime, SortOptions.Long);

            Analyze(x => x.Query, typeof(StandardAnalyzer).AssemblyQualifiedName);
        }
    }

    public class MessagesViewTransformer : AbstractTransformerCreationTask<MessagesViewIndex.Result>
    {
        public MessagesViewTransformer()
        {
            TransformResults = messages => from message in messages
                                           let metadata = message.ProcessingAttempts != null ? message.ProcessingAttempts.Last().MessageMetadata : message.MessageMetadata
                                           let headers = message.ProcessingAttempts != null ? message.ProcessingAttempts.Last().Headers : message.Headers
                                           select new
                                           {
                                               Id = message.UniqueMessageId,
                                               IsSystemMessage = metadata["IsSystemMessage"],
                                               MessageId = metadata["MessageId"],
                                               MessageType = metadata["MessageType"],
                                               MessageIntent = metadata["MessageIntent"],
                                               SendingEndpoint = metadata["SendingEndpoint"],
                                               ReceivingEndpoint = metadata["ReceivingEndpoint"],
                                               TimeSent = (DateTime)metadata["TimeSent"],
                                               ProcessedAt = message.ProcessingAttempts != null ? message.ProcessingAttempts.Last().FailureDetails.TimeOfFailure : message.ProcessedAt,
                                               ProcessingTime = metadata["ProcessingTime"],
                                               CriticalTime =  metadata["CriticalTime"],
                                               BodyUrl = metadata["BodyUrl"],
                                               BodySize = metadata["BodySize"],
                                               //the reason the we need to use a KeyValuePair<string, object> is that raven seems to interpret the values and convert them
                                               // to real types. In this case it was the NServiceBus.Temporary.DelayDeliveryWith header to was converted to a timespan
                                               Headers = headers.Select(header => new KeyValuePair<string, object>(header.Key, header.Value)),
                                               Status = message.ProcessingAttempts != null ? MessageStatus.Failed : MessageStatus.Successful
                                           };
        }
    }
}