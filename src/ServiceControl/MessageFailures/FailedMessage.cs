namespace ServiceControl.MessageFailures
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using Contracts.Operations;

    public class FailedMessage
    {
        public FailedMessage()
        {
            ProcessingAttempts = new List<ProcessingAttempt>();
        }

        public string Id { get; set; }

        public List<ProcessingAttempt> ProcessingAttempts { get; set; }
        public MessageStatus Status { get; set; }

        public ProcessingAttempt MostRecentAttempt 
        {
            get
            {
                return ProcessingAttempts.LastOrDefault();
            } 
        }

        public class ProcessingAttempt
        {
            public Dictionary<string, MessageMetadata> MessageMetadata { get; set; }

            public FailureDetails FailureDetails { get; set; }

            public EndpointDetails FailingEndpoint { get; set; }
            public DateTime AttemptedAt { get; set; }
            public string UniqueMessageId { get; set; }
        }

    }
}