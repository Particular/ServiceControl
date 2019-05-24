namespace ServiceControl.MessageFailures
{
    using System;
    using System.Collections.Generic;

    public class FailedMessage : IHaveStatus
    {
        public FailedMessage()
        {
            ProcessingAttempts = new List<ProcessingAttempt>();
        }

        public string Id { get; set; }

        public List<ProcessingAttempt> ProcessingAttempts { get; set; }

        public string UniqueMessageId { get; set; }

        public FailedMessageStatus Status { get; set; }

        public static string MakeDocumentId(string messageUniqueId)
        {
            return $"{CollectionName}/{messageUniqueId}";
        }

        public const string CollectionName = "FailedMessages";

        public class ProcessingAttempt
        {
            public ProcessingAttempt()
            {
                MessageMetadata = new Dictionary<string, object>();
                Headers = new Dictionary<string, string>();
            }
            
            public Dictionary<string, object> MessageMetadata { get; set; }
            public DateTime AttemptedAt { get; set; }
            public string MessageId { get; set; }
            public Dictionary<string, string> Headers { get; set; }
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