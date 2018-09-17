namespace ServiceControl.MessageFailures.InternalMessages
{
    using NServiceBus;

    class PerformRetry : ICommand
    {
        public string FailedMessageId { get; set; }
    }
}