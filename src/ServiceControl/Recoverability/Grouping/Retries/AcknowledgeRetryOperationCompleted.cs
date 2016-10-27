namespace ServiceControl.Recoverability
{
    using NServiceBus;

    public class AcknowledgeRetryOperationCompleted : ICommand
    {
        public string RequestId { get; set; }
        public RetryType RetryType { get; set; }
    }
}