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
                let metadata = last.MessageMetadata
                select new SortAndFilterOptions
                {
                    MessageId = last.MessageId,
                    MessageType = (string)metadata["MessageType"],
                    IsSystemMessage = (bool)metadata["IsSystemMessage"],
                    Status = message.Status == FailedMessageStatus.Archived
                        ? MessageStatus.ArchivedFailure
                          : message.Status == FailedMessageStatus.Resolved
                              ? MessageStatus.ResolvedSuccessfully
                                : message.ProcessingAttempts.Count == 1
                                    ? MessageStatus.Failed
                                    : MessageStatus.RepeatedFailure,
                    TimeSent = (DateTime)metadata["TimeSent"],
                    ProcessedAt = last.AttemptedAt,
                    ReceivingEndpointName = ((EndpointDetails)metadata["ReceivingEndpoint"]).Name,
                    CriticalTime = (TimeSpan?)metadata["CriticalTime"],
                    ProcessingTime = (TimeSpan?)metadata["ProcessingTime"],
                    DeliveryTime = (TimeSpan?)metadata["DeliveryTime"],
                    Query = new[] {
                        string.Join(' ', last.Headers.Values),
                        string.Join(' ', metadata.Values.Where(v => v != null).Select(v => v.ToString())) // Needed, RavenDB does not like object arrays
                    },
                    ConversationId = (string)metadata["ConversationId"]
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