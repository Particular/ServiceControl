namespace ServiceControl.Persistence
{
    using System;
    using System.Linq;
    using Raven.Client.Documents.Indexes;
    using ServiceControl.MessageFailures;
    using ServiceControl.Operations;

    public class MessagesViewIndex : AbstractIndexCreationTask<FailedMessage, MessagesViewIndex.SortAndFilterOptions>
    {
        public MessagesViewIndex()
        {
            Map = messages =>

                from message in messages
                let last = message.ProcessingAttempts.Last()
                select new SortAndFilterOptions
                {
                    MessageId = last.MessageId,
                    MessageType = (string)last.MessageMetadata["MessageType"],
                    IsSystemMessage = (bool)last.MessageMetadata["IsSystemMessage"],
                    Status = message.Status == FailedMessageStatus.Archived
                        ? MessageStatus.ArchivedFailure
                          : message.Status == FailedMessageStatus.Resolved
                              ? MessageStatus.ResolvedSuccessfully
                                : message.ProcessingAttempts.Count == 1
                                    ? MessageStatus.Failed
                                    : MessageStatus.RepeatedFailure,
                    TimeSent = (DateTime)last.MessageMetadata["TimeSent"],
                    ProcessedAt = last.AttemptedAt,
                    ReceivingEndpointName = ((EndpointDetails)last.MessageMetadata["ReceivingEndpoint"]).Name,
                    CriticalTime = (TimeSpan?)last.MessageMetadata["CriticalTime"],
                    ProcessingTime = (TimeSpan?)last.MessageMetadata["ProcessingTime"],
                    DeliveryTime = (TimeSpan?)last.MessageMetadata["DeliveryTime"],
                    // Dates, durations, sizes and booleans only add tokens nobody searches for, so they are excluded.
                    // Identifiers are excluded too: they are matched exactly on the MessageId/ConversationId fields instead.
                    Query = last.MessageMetadata
                        .Where(m => m.Key != "MessageId" && m.Key != "ConversationId" && m.Key != "RelatedToId"
                                && m.Key != "TimeSent" && m.Key != "CriticalTime" && m.Key != "ProcessingTime"
                                && m.Key != "DeliveryTime" && m.Key != "ContentLength" && m.Key != "BodyUrl"
                                && m.Key != "IsSystemMessage")
                        .Select(m => m.Value.ToString())
                        .Concat(last.Headers
                            .Where(h => h.Key != "NServiceBus.MessageId" && h.Key != "NServiceBus.ConversationId"
                                    && h.Key != "NServiceBus.CorrelationId" && h.Key != "NServiceBus.RelatedTo"
                                    && h.Key != "NServiceBus.TimeSent" && h.Key != "NServiceBus.ProcessingStarted" && h.Key != "NServiceBus.ProcessingEnded"
                                    && h.Key != "NServiceBus.DeliverAt" && h.Key != "NServiceBus.Timeout.Expire" && h.Key != "NServiceBus.Retries.Timestamp"
                                    && h.Key != "NServiceBus.ExceptionInfo.TimeOfFailure" && h.Key != "NServiceBus.TimeOfFailure" && h.Key != "NServiceBus.NonDurableMessage"
                                    && h.Key != "NServiceBus.TimeToBeReceived")
                            .Select(h => h.Value))
                        .Where(v => v != null && v.Length > 0)
                        .Distinct()
                        .ToArray(),
                    ConversationId = (string)last.MessageMetadata["ConversationId"]
                };

            // StandardAnalyzer is the default analyzer, so no follow-up Analyze() call is needed here
            Index(x => x.Query, FieldIndexing.Search);
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