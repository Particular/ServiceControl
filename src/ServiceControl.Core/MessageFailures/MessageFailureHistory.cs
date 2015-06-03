namespace ServiceControl.MessageFailures
{
    using System;
    using System.Collections.Generic;
    using Contracts.Operations;

    public class MessageFailureHistory
    {
        public static string MakeDocumentId(string messageUniqueId)
        {
            return "MessageFailureHistories/" + messageUniqueId;
        }

        public MessageFailureHistory()
        {
            ProcessingAttempts = new List<ProcessingAttempt>();
            FailureGroups = new List<FailureGroup>();
        }

        public string Id { get; set; }

        public List<ProcessingAttempt> ProcessingAttempts { get; set; }
        public List<FailureGroup> FailureGroups { get; set; }
        
        public FailedMessageStatus Status { get; set; }

        public string UniqueMessageId { get; set; }

        public class ProcessingAttempt
        {
            public EndpointDetails SendingEndpoint { get; set; }//new
            public EndpointDetails ProcessingEndpoint { get; set; }//new
            public string MessageType { get; set; }//new
            public string ContentType { get; set; }//new
            public DateTime TimeSent { get; set; }//new
            public FailureDetails FailureDetails { get; set; }
            public DateTime AttemptedAt { get; set; }
            public string MessageId { get; set; }
            public Dictionary<string, string> Headers { get; set; }
            public string ReplyToAddress { get; set; }
            public bool Recoverable { get; set; }
            public string CorrelationId { get; set; }
            public string MessageIntent { get; set; }//changed to string from enum
            public bool IsSystemMessage { get; set; }//new

            //removed public Dictionary<string, object> MessageMetadata { get; set; }
        }

        public class FailureGroup
        {
            public string Id { get; set; }
            public string Title { get; set; }
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
