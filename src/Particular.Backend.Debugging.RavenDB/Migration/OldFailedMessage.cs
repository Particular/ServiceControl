namespace Particular.Backend.Debugging.RavenDB.Migration
{
    using System;
    using System.Collections.Generic;
    using NServiceBus;
    using ServiceControl.Contracts.Operations;

    public class OldFailedMessage
    {
        public static string MakeDocumentId(string messageUniqueId)
        {
            return "FailedMessages/" + messageUniqueId;
        }

        public OldFailedMessage()
        {
            ProcessingAttempts = new List<ProcessingAttempt>();
        }

        public string Id { get; set; }

        public List<ProcessingAttempt> ProcessingAttempts { get; set; }

        public FailedMessageStatus Status { get; set; }

        public string UniqueMessageId { get; set; }

        public class ProcessingAttempt
        {
            public FailureDetails FailureDetails { get; set; }
            public DateTime AttemptedAt { get; set; }
            public string MessageId { get; set; }
            public Dictionary<string, string> Headers { get; set; }
            public string ReplyToAddress { get; set; }
            public bool Recoverable { get; set; }
            public string CorrelationId { get; set; }
            public MessageIntentEnum MessageIntent { get; set; }
            public Dictionary<string, object> MessageMetadata { get; set; }
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
