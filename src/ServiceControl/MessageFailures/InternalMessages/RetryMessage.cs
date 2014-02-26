namespace ServiceControl.MessageFailures.InternalMessages
{
    using NServiceBus;

    public class RetryMessage : ICommand
    {
        public string FailedMessageId { get; set; }
    }
}