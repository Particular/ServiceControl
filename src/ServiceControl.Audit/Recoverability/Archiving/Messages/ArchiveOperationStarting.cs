namespace ServiceControl.Recoverability
{
    using System;
    using Infrastructure.DomainEvents;
    using Infrastructure.SignalR;

    public class ArchiveOperationStarting : IDomainEvent, IUserInterfaceEvent
    {
        public string RequestId { get; set; }
        public ArchiveType ArchiveType { get; set; }
        public ArchiveProgress Progress { get; set; }
        public DateTime StartTime { get; set; }
    }
}