namespace ServiceControl.Recoverability
{
    using System;
    using ServiceControl.Infrastructure.DomainEvents;
    using ServiceControl.Infrastructure.SignalR;

    public class ArchiveOperationBatchCompleted : IDomainEvent, IUserInterfaceEvent
    {
        public string RequestId { get; set; }
        public ArchiveType ArchiveType { get; set; }
        public ArchiveProgress Progress { get; set; }
        public DateTime StartTime { get; set; }
        public DateTime Last { get; set; }
    }
}