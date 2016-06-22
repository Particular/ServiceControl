namespace ServiceControl.MessageFailures.Api
{
    using System;
    using System.Linq;
    using Contracts.Operations;
    using MessageFailures;
    using Raven.Client.Indexes;

    public class FailedMessageViewIndex : AbstractIndexCreationTask<FailedMessage>
    {
        public class SortAndFilterOptions: IHaveStatus
        {
            public string MessageId { get; set; }
            public DateTime TimeSent { get; set; }
            public string MessageType { get; set; }
            public FailedMessageStatus Status { get; set; }
            public string ReceivingEndpointName { get; set; }
            public string FailedQueueAddress { get; set; }
            public DateTime TimeOfFailure { get; set; }
            public long LastModified { get; set; }
        }

        public FailedMessageViewIndex()
        {
            Map = messages => from message in messages
                let processingAttemptsLast = message.ProcessingAttempts.Last()
                select new
                {
                    MessageId = processingAttemptsLast.MessageMetadata["MessageId"],
                    MessageType = processingAttemptsLast.MessageMetadata["MessageType"],
                    message.Status,
                    TimeSent = (DateTime) processingAttemptsLast.MessageMetadata["TimeSent"],
                    ReceivingEndpointName = ((EndpointDetails) processingAttemptsLast.MessageMetadata["ReceivingEndpoint"]).Name,
                    FailedQueueAddress = processingAttemptsLast.FailureDetails.AddressOfFailingEndpoint,
                    TimeOfFailure = processingAttemptsLast.FailureDetails.TimeOfFailure,
                    LastModified = MetadataFor(message).Value<DateTime>("Last-Modified").Ticks
                };

            DisableInMemoryIndexing = true;
        }
    }
}