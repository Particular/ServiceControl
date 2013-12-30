namespace ServiceControl.MessageFailures
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using Contracts.Operations;
    using NServiceBus;

    public class FailedMessage
    {
        public FailedMessage()
        {
            ProcessingAttempts = new List<ProcessingAttempt>();
        }

        public string Id { get; set; }

        public List<ProcessingAttempt> ProcessingAttempts { get; set; }
        public FailedMessageStatus Status { get; set; }

        public ProcessingAttempt MostRecentAttempt 
        {
            get
            {
                return ProcessingAttempts.LastOrDefault();
            } 
        }

        public class ProcessingAttempt
        {
            public Dictionary<string, object> MessageMetadata { get; set; }
            public FailureDetails FailureDetails { get; set; }
            public DateTime AttemptedAt { get; set; }
            public string MessageId { get; set; }
            public Dictionary<string, string> Headers { get; set; }
            public string ReplyToAddress { get; set; }
            public bool Recoverable { get; set; }
            public string CorrelationId { get; set; }
            public MessageIntentEnum MessageIntent { get; set; }
        }

    }

    public enum FailedMessageStatus
    {
        Unresolved = 1,
        Resolved = 2,
        RetryIssued = 3,
        Archived = 4
    }
}