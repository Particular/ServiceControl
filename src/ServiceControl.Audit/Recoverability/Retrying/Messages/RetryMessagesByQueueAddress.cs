namespace ServiceControl.Recoverability
{
    using MessageFailures;
    using NServiceBus;

    public class RetryMessagesByQueueAddress : ICommand
    {
        public string QueueAddress { get; set; }
        public FailedMessageStatus Status { get; set; }
    }
}