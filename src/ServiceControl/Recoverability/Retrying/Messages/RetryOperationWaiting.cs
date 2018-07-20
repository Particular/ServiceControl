namespace ServiceControl.Recoverability
{
    using System;
    using Infrastructure.DomainEvents;
    using Infrastructure.SignalR;

    public class RetryOperationWaiting : IDomainEvent, IUserInterfaceEvent
    {
        public string RequestId { get; set; }
        public RetryType RetryType { get; set; }
        public RetryProgress Progress { get; set; }
        public DateTime StartTime { get; set; }
    }
}