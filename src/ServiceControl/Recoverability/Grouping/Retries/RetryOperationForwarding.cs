namespace ServiceControl.Recoverability
{
    using NServiceBus;

    public class RetryOperationForwarding : IEvent
    {
        public string RequestId { get; set; }
        public RetryType RetryType { get; set; }
        public int TotalNumberOfMessages { get; set; }
        public Progression Progression { get; set; }
    }
}