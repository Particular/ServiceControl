namespace ServiceControl.Recoverability
{
    using NServiceBus;
    using ServiceControl.MessageFailures;

    public class RetryMessagesByQueueAddress : ICommand
    {
        public string QueueAddress { get; set; }
        public FailedMessageStatus Status { get; set; }
    }
}
