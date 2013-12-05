namespace ServiceControl.MessageFailures
{
    using System;
    using System.Collections.Generic;
    using Contracts.Operations;
    using ServiceBus.Management.MessageAuditing;

    public class FailedMessage
    {
        public FailedMessage()
        {
            ProcessingAttempts = new List<ProcessingAttempt>();
        }

        public string Id { get; set; }

        public string MessageId { get; set; }

        public List<ProcessingAttempt> ProcessingAttempts { get; set; }
        public MessageStatus Status { get; set; }

        public class ProcessingAttempt
        {
            public Message2 Message { get; set; }
            public FailureDetails FailureDetails { get; set; }
            public DateTime AttemptedAt { get; set; }

        }

    }
}