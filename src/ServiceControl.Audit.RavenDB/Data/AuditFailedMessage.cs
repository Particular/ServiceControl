namespace ServiceControl.MessageAuditing
{
    using System;
    using System.Collections.Generic;
    using Contracts.Operations;
    using NServiceBus;

    public class AuditFailedMessage
    {
        public static string MakeDocumentId(string messageUniqueId)
        {
            return "AuditFailedMessages/" + messageUniqueId;
        }

        public string Id { get; set; }
        public ProcessingAttempt LastProcessingAttempt { get; set; }
        public MessageStatus Status { get; set; }
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
    }
}
