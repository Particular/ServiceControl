namespace ServiceControl.MessageFailures.Api
{
    using System;
    using Contracts.Operations;

    public class FailedMessageView
    {
        public string Id { get; set; }
        public string ReceivingEndpointName { get; set; }
        public string MessageType { get; set; }
        public DateTime TimeSent { get; set; }
        public DateTime TimeOfFailure { get; set; }
        public bool IsSystemMessage { get; set; }

        public ExceptionDetails Exception { get; set; }

        public string MessageId { get; set; }
        public int NumberOfProcessingAttempts { get; set; }

        public FailedMessageStatus Status { get; set; }
        public EndpointDetails SendingEndpoint { get; set; }
        public EndpointDetails ReceivingEndpoint { get; set; }
    }
}