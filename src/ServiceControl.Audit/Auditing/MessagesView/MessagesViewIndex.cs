namespace ServiceControl.Audit.Auditing.MessagesView
{
    using System;
    using System.Linq;
    using Lucene.Net.Analysis.Standard;
    using Monitoring;
    using Raven.Client.Documents.Indexes;

    public class MessagesViewIndex : AbstractIndexCreationTask<ProcessedMessage, MessagesViewIndex.Result>
    {
        public MessagesViewIndex()
        {
            Map = messages => from message in messages
                select new Result
                {
                    MessageId = message.MessageMetadata.MessageId,
                    MessageType = message.MessageMetadata.MessageType,
                    IsSystemMessage = message.MessageMetadata.IsSystemMessage,
                    Status = message.MessageMetadata.IsRetried ? MessageStatus.ResolvedSuccessfully : MessageStatus.Successful,
                    TimeSent = message.MessageMetadata.TimeSent,
                    ProcessedAt = message.ProcessedAt,
                    ReceivingEndpointName = message.MessageMetadata.ReceivingEndpoint != null ? message.MessageMetadata.ReceivingEndpoint.Name : null,
                    CriticalTime = message.MessageMetadata.CriticalTime,
                    ProcessingTime = message.MessageMetadata.ProcessingTime,
                    DeliveryTime = message.MessageMetadata.DeliveryTime,
                    Query = message.Headers.Select(x => x.Value).ToArray(),
                    ConversationId = message.MessageMetadata.ConversationId
                };

            Index(x => x.Query, FieldIndexing.Search);
            Analyze(x => x.Query, typeof(StandardAnalyzer).FullName);
        }

        public class Result
        {
            public string MessageId { get; set; }
            public string MessageType { get; set; }
            public bool IsSystemMessage { get; set; }
            public MessageStatus Status { get; set; }
            public DateTime ProcessedAt { get; set; }
            public string ReceivingEndpointName { get; set; }
            public TimeSpan? CriticalTime { get; set; }
            public TimeSpan? ProcessingTime { get; set; }
            public TimeSpan? DeliveryTime { get; set; }
            public string ConversationId { get; set; }
            public string[] Query { get; set; }
            public DateTime? TimeSent { get; set; }
        }
    }
}