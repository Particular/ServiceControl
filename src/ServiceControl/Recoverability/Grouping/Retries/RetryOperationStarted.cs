namespace ServiceControl.Recoverability
{
    using NServiceBus;

    public class RetryOperationStarted : IEvent
    {
        public string RequestId { get; set; }
        public RetryType RetryType { get; set; }
        public int NumberOfMessages { get; set; }
    }
}