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
            public TimeSpan DeliveryTime { get; set; }
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
                DeliveryTime = message.MessageMetadata["DeliveryTime"],
                Query = message.MessageMetadata.Select(kvp => kvp.Value.ToString())
            }));


            AddMap<FailedMessage>(messages => messages.Where(fm => fm.Status != FailedMessageStatus.Resolved)
                .Select(message => new
            {
                MessageId = message.ProcessingAttempts.Last().MessageMetadata["MessageId"],
                ConversationId = message.ProcessingAttempts.Last().MessageMetadata["ConversationId"],
                MessageType = message.ProcessingAttempts.Last().MessageMetadata["MessageType"],
                MessageIntent = message.ProcessingAttempts.Last().MessageMetadata["MessageIntent"],
                IsSystemMessage = message.ProcessingAttempts.Last().MessageMetadata["IsSystemMessage"],
                Status = message.ProcessingAttempts.Count() == 1 ? MessageStatus.Failed : MessageStatus.RepeatedFailure,
                TimeSent = (DateTime)message.ProcessingAttempts.Last().MessageMetadata["TimeSent"],
                ProcessedAt = message.ProcessingAttempts.Last().AttemptedAt,
                ReceivingEndpointName = ((EndpointDetails)message.ProcessingAttempts.Last().MessageMetadata["ReceivingEndpoint"]).Name,
                CriticalTime = (object) TimeSpan.Zero,
                ProcessingTime = (object) TimeSpan.Zero,
                DeliveryTime = (object) TimeSpan.Zero,
                Query = message.ProcessingAttempts.Last().MessageMetadata.Select(kvp => kvp.Value.ToString())
            }));

            Index(x => x.Query, FieldIndexing.Analyzed);
            Index(x => x.CriticalTime, FieldIndexing.Default);
            Index(x => x.ProcessingTime, FieldIndexing.Default);
            Index(x => x.ProcessedAt, FieldIndexing.Default);
            Index(x => x.DeliveryTime, FieldIndexing.Default);

            Sort(x => x.CriticalTime, SortOptions.Long);
            Sort(x => x.ProcessingTime, SortOptions.Long);
            Sort(x => x.DeliveryTime, SortOptions.Long);

            Analyze(x => x.Query, typeof(StandardAnalyzer).AssemblyQualifiedName);
        }
    }
}