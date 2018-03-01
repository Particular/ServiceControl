namespace ServiceControl.Recoverability
{
    using System;
    using ServiceControl.Infrastructure.DomainEvents;
    using ServiceControl.Infrastructure.SignalR;

    public class ArchiveOperationCompleted : IDomainEvent, IUserInterfaceEvent
    {
        public string RequestId { get; set; }
        public string GroupName { get; set; }
        public ArchiveType ArchiveType { get; set; }
        public ArchiveProgress Progress { get; set; }
        public DateTime StartTime { get; set; }
        public DateTime Last { get; set; }
        public DateTime CompletionTime { get; set; }
    }
}