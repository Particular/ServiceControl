namespace ServiceBus.Management
{
    using System;
    using System.Collections.Generic;

    public class Message
    {
        public string Id { get; set; }

        public string MessageType { get; set; }

        public IEnumerable<KeyValuePair<string, string>> Headers { get; set; }

        public string Body { get; set; }

        public byte[] BodyRaw { get; set; }

        public string RelatedToMessageId { get; set; }

        public string CorrelationId { get; set; }

        public string ConversationId { get; set; }

        public MessageStatus Status { get; set; }

        public string Endpoint { get; set; }

        public FailureDetails FailureDetails { get; set; }

        public DateTime TimeSent { get; set; }
    }

    public class FailureDetails
    {
        public int NumberOfTimesFailed { get; set; }

        public string FailedInQueue { get; set; }

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
}