namespace ServiceControl.MessageFailures.InternalMessages
{
    using System;
    using NServiceBus;

    public class ArchiveMessage : ICommand
    {
        public string FailedMessageId { get; set; }
    }

    public class RetryMessage : ICommand
    {
        public string FailedMessageId { get; set; }
    }

    public class PerformRetry : ICommand
    {
        public string FailedMessageId { get; set; }
        public Address TargetEndpointAddress { get; set; }
        public Guid RetryId { get; set; }
    }
}