namespace ServiceControl.MessageFailures
{
    using System;
    using System.Collections.Generic;
    using Contracts.Operations;

    public class FailedMessage
    {
        public static string MakeDocumentId(string messageUniqueId)
        {
            return "FailedMessages/" + messageUniqueId;
        }

        public FailedMessage()
        {
            ProcessingAttempts = new List<ProcessingAttempt>();
        }

        public string Id { get; set; }

        public List<ProcessingAttempt> ProcessingAttempts { get; set; }
        
        public FailedMessageStatus Status { get; set; }

        public string UniqueMessageId { get; set; }

        public class ProcessingAttempt
        {
            public EndpointDetails SendingEndpoint { get; set; }
            public EndpointDetails ProcessingEndpoint { get; set; }
            public string MessageType { get; set; }
            public string ContentType { get; set; }
            public DateTime TimeSent { get; set; }
            public FailureDetails FailureDetails { get; set; }
            public DateTime AttemptedAt { get; set; }
            public string MessageId { get; set; }
            public Dictionary<string, string> Headers { get; set; }
            public string ReplyToAddress { get; set; }
            public bool Recoverable { get; set; }
            public string CorrelationId { get; set; }
            public string MessageIntent { get; set; }
            public bool IsSystemMessage { get; set; }
            public string HeadersForSearching { get; set; }
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
