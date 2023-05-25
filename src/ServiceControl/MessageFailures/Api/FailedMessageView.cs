﻿namespace ServiceControl.MessageFailures.Api
{
    using System;
    using Contracts.Operations;
    using ServiceControl.Operations;

    public class FailedMessageView
    {
        public string Id { get; set; }
        public string MessageType { get; set; }
        public DateTime? TimeSent { get; set; }
        public bool IsSystemMessage { get; set; }
        public ExceptionDetails Exception { get; set; }
        public string MessageId { get; set; }
        public int NumberOfProcessingAttempts { get; set; }
        public FailedMessageStatus Status { get; set; }
        public EndpointDetails SendingEndpoint { get; set; }
        public EndpointDetails ReceivingEndpoint { get; set; }
        public string QueueAddress { get; set; }
        public DateTime TimeOfFailure { get; set; }
        public DateTime LastModified { get; set; }
        public bool Edited { get; set; }
        public string EditOf { get; set; }
    }
}