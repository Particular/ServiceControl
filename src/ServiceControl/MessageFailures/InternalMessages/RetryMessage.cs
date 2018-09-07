namespace ServiceControl.MessageFailures.InternalMessages
{
    using NServiceBus;

    class RetryMessage : ICommand
    {
        public string FailedMessageId { get; set; }
    }
}