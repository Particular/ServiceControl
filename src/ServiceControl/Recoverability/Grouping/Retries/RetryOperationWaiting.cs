namespace ServiceControl.Recoverability
{
    using NServiceBus;

    public class RetryOperationWaiting : IEvent
    {
        public string RequestId { get; set; }
        public RetryType RetryType { get; set; }
        public Progression Progression { get; set; }
    }
}