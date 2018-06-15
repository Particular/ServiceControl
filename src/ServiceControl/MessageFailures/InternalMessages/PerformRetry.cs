namespace ServiceControl.MessageFailures.InternalMessages
{
    using NServiceBus;

    public class PerformRetry : ICommand
    {
        public string FailedMessageId { get; set; }
    }
}