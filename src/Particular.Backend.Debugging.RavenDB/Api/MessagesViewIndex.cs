namespace Particular.Backend.Debugging.RavenDB.Api
{
    using System;
    using System.Linq;
    using Lucene.Net.Analysis.Standard;
    using Particular.Backend.Debugging.RavenDB.Model;
    using Particular.Backend.Debugging.RavenDB.Storage;
    using Raven.Abstractions.Indexing;
    using Raven.Client.Indexes;
    using ServiceControl.Contracts.Operations;

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
            public TimeSpan? CriticalTime { get; set; }
            public TimeSpan? ProcessingTime { get; set; }
            public TimeSpan? DeliveryTime { get; set; }
            public string ConversationId { get; set; }
            public string[] Query { get; set; }
            public DateTime TimeSent { get; set; }
        }

        public MessagesViewIndex()
        {
            AddMap<MessageSnapshotDocument>(messages => from message in messages
                let q = new[]
                {
                    message.MessageId,
                    message.MessageType,
                    message.SendingEndpoint.Name,
                    message.ReceivingEndpoint.Name,
                    message.Body != null ? message.Body.Text : ""
                }
                select new SortAndFilterOptions
                {
                    MessageId = message.MessageId,
                    MessageType = message.MessageType,
                    IsSystemMessage = message.IsSystemMessage,
                    Status = message.Status,
                    TimeSent = message.Processing.TimeSent,
                    ProcessedAt = message.AttemptedAt,
                    ReceivingEndpointName = message.ReceivingEndpoint.Name,
                    CriticalTime = message.Processing.CriticalTime,
                    ProcessingTime = message.Processing.ProcessingTime,
                    DeliveryTime = message.Processing.DeliveryTime,
                    Query = q,
                    ConversationId = message.ConversationId,
                });

            Index(x => x.Query, FieldIndexing.Analyzed);
            
            Analyze(x => x.Query, typeof(StandardAnalyzer).AssemblyQualifiedName);

            DisableInMemoryIndexing = true;
        }
    }
}