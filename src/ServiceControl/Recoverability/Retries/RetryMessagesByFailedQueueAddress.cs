namespace ServiceControl.Recoverability
{
    using NServiceBus;

    public class RetryMessagesByFailedQueueAddress : ICommand
    {
        public string FailedQueueAddress { get; set; }
    }
}
