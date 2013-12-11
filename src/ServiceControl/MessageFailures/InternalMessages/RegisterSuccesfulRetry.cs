namespace ServiceControl.MessageFailures.InternalMessages
{
    using System;
    using NServiceBus;

    public class RegisterSuccesfulRetry :ICommand
    {
        public string FailedMessageId { get; set; }
        public Guid RetryId { get; set; }
    }
}