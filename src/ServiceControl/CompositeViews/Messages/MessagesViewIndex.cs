namespace ServiceControl.CompositeViews.Messages
{
    using System;
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
            public string MessageType { get; set; }
            public bool IsSystemMessage { get; set; }
            public MessageStatus Status { get; set; }
            public DateTime ProcessedAt { get; set; }
            public string ReceivingEndpointName { get; set; }
            public TimeSpan CriticalTime { get; set; }
            public TimeSpan ProcessingTime { get; set; }
            public TimeSpan DeliveryTime { get; set; }
            public string ConversationId { get; set; }
            public string[] Query { get; set; }
            public DateTime TimeSent { get; set; }
        }

        public MessagesViewIndex()
        {
            AddMap<ProcessedMessage>(messages => from message in messages
                let resolved = LoadDocument<FailedMessage>("FailedMessages/" + message.UniqueMessageId)
                select new
                {
                    MessageId = message.MessageMetadata["MessageId"],
                    MessageType = message.MessageMetadata["MessageType"],
                    IsSystemMessage = message.MessageMetadata["IsSystemMessage"],
                    Status = resolved == null ? MessageStatus.Successful : MessageStatus.ResolvedSuccessfully,
                    TimeSent = (DateTime) message.MessageMetadata["TimeSent"],
                    message.ProcessedAt,
                    ReceivingEndpointName = ((EndpointDetails) message.MessageMetadata["ReceivingEndpoint"]).Name,
                    CriticalTime = message.MessageMetadata["CriticalTime"],
                    ProcessingTime = message.MessageMetadata["ProcessingTime"],
                    DeliveryTime = message.MessageMetadata["DeliveryTime"],
                    Query = message.MessageMetadata.Select(_ => _.Value.ToString()),
                    ConversationId = message.MessageMetadata["ConversationId"],
                });


            AddMap<FailedMessage>(messages => from message in messages
                where message.Status != FailedMessageStatus.Resolved
                let last = message.ProcessingAttempts.Last()
                let status =
                    message.Status == FailedMessageStatus.Archived
                        ? MessageStatus.ArchivedFailure
                        : message.ProcessingAttempts.Count == 1
                            ? MessageStatus.Failed
                            : MessageStatus.RepeatedFailure
                select new
                {
                    MessageId = (object) last.MessageId,
                    MessageType = last.MessageMetadata["MessageType"], 
                    IsSystemMessage = last.MessageMetadata["IsSystemMessage"],
                    Status = status,
                    TimeSent = (DateTime) last.MessageMetadata["TimeSent"],
                    ProcessedAt = last.AttemptedAt,
                    ReceivingEndpointName = ((EndpointDetails) last.MessageMetadata["ReceivingEndpoint"]).Name,
                    CriticalTime = (object) TimeSpan.Zero,
                    ProcessingTime = (object) TimeSpan.Zero,
                    DeliveryTime = (object) TimeSpan.Zero,
                    Query = last.MessageMetadata.Select(_ => _.Value.ToString()),
                    ConversationId = last.MessageMetadata["ConversationId"],
                });

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