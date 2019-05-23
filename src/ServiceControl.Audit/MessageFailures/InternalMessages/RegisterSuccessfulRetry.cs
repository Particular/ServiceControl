namespace ServiceControl.MessageFailures.InternalMessages
{
    using System;
    using NServiceBus;

    class RegisterSuccessfulRetry : ICommand
    {
        public string FailedMessageId { get; set; }
        public Guid RetryId { get; set; }
    }
}