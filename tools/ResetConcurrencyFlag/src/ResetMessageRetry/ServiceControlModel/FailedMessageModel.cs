using System;
using System.Collections.Generic;

namespace ResetMessageRetry
{
    public class FailedMessage
    {
        public static string MakeDocumentId(string messageUniqueId)
        {
            return "FailedMessages/" + messageUniqueId;
        }

        public FailedMessage()
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

        public class FailureDetails
        {
            public string AddressOfFailingEndpoint { get; set; }

            public DateTime TimeOfFailure { get; set; }

            public ExceptionDetails Exception { get; set; }

        }

        public class ExceptionDetails
        {
            public string ExceptionType { get; set; }
            public string Message { get; set; }
            public string Source { get; set; }
            public string StackTrace { get; set; }
        }

        public enum MessageIntentEnum
        {
            Init,
            Send,
            Publish,
            Subscribe,
            Unsubscribe,
            Reply,
        }

        public class FailureGroup
        {
            public string Id { get; set; }
            public string Title { get; set; }
            public string Type { get; set; }
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