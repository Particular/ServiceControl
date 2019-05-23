namespace ServiceControl.Recoverability
{
    using System;
    using Infrastructure.DomainEvents;
    using Infrastructure.SignalR;

    public class RetryOperationForwarding : IDomainEvent, IUserInterfaceEvent
    {
        public string RequestId { get; set; }
        public RetryType RetryType { get; set; }
        public int TotalNumberOfMessages { get; set; }
        public RetryProgress Progress { get; set; }
        public bool IsFailed { get; set; }
        public DateTime StartTime { get; set; }
    }
}