namespace ServiceControl.MessageFailures.Api
{
    using System;
    using System.Linq;
    using MessageFailures;
    using Raven.Client.Indexes;

    public class FailedMessageViewIndex : AbstractIndexCreationTask<FailedMessage>
    {
        public class SortAndFilterOptions
        {
            public string MessageId { get; set; }
            public DateTime TimeSent { get; set; }
            public string MessageType { get; set; }
            public FailedMessageStatus Status { get; set; }
            public string ReceivingEndpointName { get; set; }
        }

        public FailedMessageViewIndex()
        {
            Map = messages => from message in messages
                              let last = message.ProcessingAttempts.Last()
           select new
            {
                MessageId = last.MessageId,
                MessageType = last.MessageType, 
                message.Status,
                TimeSent = last.TimeSent,
                ReceivingEndpointName = last.ProcessingEndpoint.Name,
            };

            DisableInMemoryIndexing = true;
        }
    }
}