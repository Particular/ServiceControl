namespace ServiceControl.Recoverability
{
    using System;
    using ServiceControl.Infrastructure.DomainEvents;
    using ServiceControl.Infrastructure.SignalR;

    public class RetryOperationWaiting : IDomainEvent, IUserInterfaceEvent
    {
        public string RequestId { get; set; }
        public RetryType RetryType { get; set; }
        public RetryProgress Progress { get; set; }
        public DateTime StartTime { get; set; }
    }
}