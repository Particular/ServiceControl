namespace ServiceControl.Contracts.Failures
{
    using System;
    using System.Collections.Generic;

    public class MessageFailed
    {
        public string FailedMessageId { get; set; }
        public string MessageType { get; set; }
        public int NumberOfProcessingAttempts { get; set; }
        public MessageStatus Status { get; set; }

        public ProcessingDetails ProcessingDetails { get; set; }
        public FailureDetails FailureDetails { get; set; }
        public MessageDetails MessageDetails { get; set; }
    }

    public class ProcessingDetails
    {
        public string MessageId { get; set; }
        public EndpointDetails SendingEndpoint { get; set; }
        public EndpointDetails ProcessingEndpoint { get; set; }
    }

    public class MessageDetails
    {
        public Dictionary<string, string> Headers { get; set; }
        public string ContentType { get; set; }
        public string Body { get; set; }
        public string BodyUrl { get; set; }
    }

    public class EndpointDetails
    {
        public string Name { get; set; }
        public Guid HostId { get; set; }
        public string Host { get; set; }
    }
}