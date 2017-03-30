namespace ServiceControl.Recoverability
{
    using System;
    using NServiceBus;

    public class RetryOperationWaiting : IEvent
    {
        public string RequestId { get; set; }
        public RetryType RetryType { get; set; }
        public RetryProgress Progress { get; set; }
        public DateTime StartTime { get; set; }
    }
}