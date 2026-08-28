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
                select new SortAndFilterOptions
                {
                    MessageId = (string)message.MessageMetadata["MessageId"],
                    MessageType = (string)message.MessageMetadata["MessageType"],
                    IsSystemMessage = (bool)message.MessageMetadata["IsSystemMessage"],
                    Status = (bool)message.MessageMetadata["IsRetried"] ? MessageStatus.ResolvedSuccessfully : MessageStatus.Successful,
                    TimeSent = (DateTime)message.MessageMetadata["TimeSent"],
                    ProcessedAt = message.ProcessedAt,
                    ReceivingEndpointName = ((EndpointDetails)message.MessageMetadata["ReceivingEndpoint"]).Name,
                    SendingEndpointName = ((EndpointDetails)message.MessageMetadata["SendingEndpoint"]).Name,
                    CriticalTime = (TimeSpan?)message.MessageMetadata["CriticalTime"],
                    ProcessingTime = (TimeSpan?)message.MessageMetadata["ProcessingTime"],
                    DeliveryTime = (TimeSpan?)message.MessageMetadata["DeliveryTime"],
                    // Dates, durations, sizes and booleans only add tokens nobody searches for, so they are excluded.
                    // Identifiers (message/conversation/correlation ids) are kept: /messages/search/{id} relies on them.
                    Query = message.MessageMetadata
                        .Where(m => m.Key != "TimeSent" && m.Key != "CriticalTime" && m.Key != "ProcessingTime"
                                && m.Key != "DeliveryTime" && m.Key != "ContentLength" && m.Key != "BodyUrl"
                                && m.Key != "IsSystemMessage" && m.Key != "IsRetried" && m.Key != "BodyNotStored"
                                && m.Key != "OriginatesFromSaga")
                        .Select(m => m.Value.ToString())
                        .Concat(message.Headers
                            .Where(h => h.Key != "NServiceBus.TimeSent" && h.Key != "NServiceBus.ProcessingStarted" && h.Key != "NServiceBus.ProcessingEnded"
                                    && h.Key != "NServiceBus.DeliverAt" && h.Key != "NServiceBus.Timeout.Expire" && h.Key != "NServiceBus.Retries.Timestamp"
                                    && h.Key != "NServiceBus.ExceptionInfo.TimeOfFailure" && h.Key != "NServiceBus.TimeOfFailure" && h.Key != "NServiceBus.NonDurableMessage"
                                    && h.Key != "NServiceBus.TimeToBeReceived")
                            .Select(h => h.Value))
                        .Where(v => v != null && v.Length > 0)
                        .Distinct()
                        .ToArray(),
                    ConversationId = (string)message.MessageMetadata["ConversationId"]
                };

            Index(x => x.Query, FieldIndexing.Search);

            // Any change to this index definition (map or analyzer) causes existing audit databases to rebuild the index on startup.
            // The analyzer name deliberately does not use typeof() to prevent a dependency on Lucene.
            Analyze(x => x.Query, "StandardAnalyzer");
        }

        public class SortAndFilterOptions
        {
            public string MessageId { get; set; }
            public string MessageType { get; set; }
            public bool IsSystemMessage { get; set; }
            public MessageStatus Status { get; set; }
            public DateTime ProcessedAt { get; set; }
            public string ReceivingEndpointName { get; set; }
            public string SendingEndpointName { get; set; }
            public TimeSpan? CriticalTime { get; set; }
            public TimeSpan? ProcessingTime { get; set; }
            public TimeSpan? DeliveryTime { get; set; }
            public string ConversationId { get; set; }
            public string[] Query { get; set; }
            public DateTime TimeSent { get; set; }
        }
    }
}