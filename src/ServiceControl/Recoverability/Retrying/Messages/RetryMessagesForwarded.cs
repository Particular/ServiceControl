namespace ServiceControl.Recoverability
{
    using System;
    using ServiceControl.Infrastructure.DomainEvents;
    using ServiceControl.Infrastructure.SignalR;

    public class RetryMessagesForwarded : IDomainEvent, IUserInterfaceEvent
    {
        public string RequestId { get; set; }
        public RetryType RetryType { get; set; }
        public int TotalNumberOfMessages { get; set; }
        public RetryProgress Progress { get; set; }
        public bool IsFailed { get; set; }
        public DateTime StartTime { get; set; }
    }
}