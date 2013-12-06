namespace ServiceControl.MessageFailures
{
    using System;
    using System.Collections.Generic;
    using Contracts.Operations;
    using MessageAuditing;

    public class FailedMessage
    {
        public FailedMessage()
        {
            ProcessingAttempts = new List<ProcessingAttempt>();
        }

        public string Id { get; set; }

        public List<ProcessingAttempt> ProcessingAttempts { get; set; }
        public MessageStatus Status { get; set; }
        public string MessageId { get; set; }

        public class ProcessingAttempt
        {
            public PhysicalMessage Message { get; set; }
            public FailureDetails FailureDetails { get; set; }
            public DateTime AttemptedAt { get; set; }

        }

    }
}