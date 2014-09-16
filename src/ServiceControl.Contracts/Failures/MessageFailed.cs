namespace ServiceControl.Contracts.Failures
{
    using System;
    using System.Collections.Generic;

    public class MessageFailed
    {
        public string EntityId { get; set; }
        public string MessageId { get; set; }
        public string CorrelationId { get; set; }
        public string MessageType { get; set; }
        public bool IsSystemMessage { get; set; }
        public int NumberOfProcessingAttempts { get; set; }
        public MessageStatus Status { get; set; }
        public List<FailureDetails> FailureDetails { get; set; }
        public Dictionary<string, string> Headers { get; set; }
        public String Body { get; set; }
    }
}