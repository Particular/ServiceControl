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
                    MessageId = message.MessageId,
                    MessageType = message.MessageType,
                    IsSystemMessage = message.IsSystemMessage,
                    Status = message.Status,
                    TimeSent = message.TimeSent,
                    ProcessedAt = message.ProcessedAt,
                    ReceivingEndpointName = message.ReceivingEndpoint.Name, //TODO: what if ReceivingEndpoint is null
                    CriticalTime = message.CriticalTime,
                    ProcessingTime = message.ProcessingTime,
                    DeliveryTime = message.DeliveryTime,
                    Query = message.SearchTerms.Select(_ => _.Value.ToString()).Union(new[] {string.Join(" ", message.Headers.Select(x => x.Value))}).ToArray(),
                    ConversationId = message.ConversationId
                };

            Index(x => x.Query, FieldIndexing.Search);
            Analyze(x => x.Query, typeof(StandardAnalyzer).AssemblyQualifiedName);
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