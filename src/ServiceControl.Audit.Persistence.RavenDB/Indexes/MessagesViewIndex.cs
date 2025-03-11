namespace ServiceControl.Audit.Persistence.RavenDB.Indexes
{
    using System;
    using System.Linq;
    using Raven.Client.Documents.Indexes;
    using ServiceControl.Audit.Auditing;
    using ServiceControl.Audit.Monitoring;

    public class MessagesViewIndex : AbstractIndexCreationTask<ProcessedMessage, MessagesViewIndex.SortAndFilterOptions>
    {
        public MessagesViewIndex()
        {
            Map = messages =>
                from message in messages
                let metadata = message.MessageMetadata
                select new SortAndFilterOptions
                {
                    MessageId = (string)metadata["MessageId"],
                    MessageType = (string)metadata["MessageType"],
                    IsSystemMessage = (bool)metadata["IsSystemMessage"],
                    Status = (bool)metadata["IsRetried"] ? MessageStatus.ResolvedSuccessfully : MessageStatus.Successful,
                    TimeSent = (DateTime)metadata["TimeSent"],
                    ProcessedAt = message.ProcessedAt,
                    ReceivingEndpointName = ((EndpointDetails)metadata["ReceivingEndpoint"]).Name,
                    CriticalTime = (TimeSpan?)metadata["CriticalTime"],
                    ProcessingTime = (TimeSpan?)metadata["ProcessingTime"],
                    DeliveryTime = (TimeSpan?)metadata["DeliveryTime"],
                    Query = new[]
                    {
                        string.Join(' ', message.Headers.Values),
                        string.Join(' ', metadata.Values.Where(v => v != null).Select(v => v.ToString())) // Needed, RaveDB does not like object arrays
                    },
                    ConversationId = (string)metadata["ConversationId"]
                };

            Index(x => x.Query, FieldIndexing.Search);

            // Not using typeof() to prevent dependency on Lucene.
            // Unfortunately while "StandardAnalyzer" would probably be better and more future-proof here,
            // we can't change this string without causing any existing audit database to completely rebuild this index.
            // If this index *must* be changed for some other reason, the analyzer name should be changed at the same time.
            Analyze(x => x.Query, "Lucene.Net.Analysis.Standard.StandardAnalyzer, Lucene.Net, Version=3.0.3.0, Culture=neutral, PublicKeyToken=85089178b9ac3181");
        }

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
    }
}