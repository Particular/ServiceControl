namespace ServiceControl.Recoverability
{
    using System;
    using Infrastructure.DomainEvents;
    using ServiceControl.Persistence;

    public class RetryOperationWaiting : IDomainEvent
    {
        public string RequestId { get; set; }
        public RetryType RetryType { get; set; }
        public RetryProgress Progress { get; set; }
        public DateTime StartTime { get; set; }
    }
}