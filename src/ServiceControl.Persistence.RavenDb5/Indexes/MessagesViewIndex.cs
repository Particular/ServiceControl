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
                    Query = last.MessageMetadata.Select(_ => _.Value.ToString()).Union(new[] { string.Join(" ", last.Headers.Select(x => x.Value)) }).ToArray(),
                    ConversationId = (string)last.MessageMetadata["ConversationId"]
                };

            Index(x => x.Query, FieldIndexing.Search);

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
            public TimeSpan? CriticalTime { get; set; }
            public TimeSpan? ProcessingTime { get; set; }
            public TimeSpan? DeliveryTime { get; set; }
            public string ConversationId { get; set; }
            public string[] Query { get; set; }
            public DateTime TimeSent { get; set; }
        }
    }
}