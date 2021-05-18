// unset

namespace ServiceControl.Recoverability
{
    using System;
    using Infrastructure.DomainEvents;
    using Infrastructure.SignalR;

    public class UnarchiveOperationStarting : IDomainEvent, IUserInterfaceEvent
    {
        public string RequestId { get; set; }
        public ArchiveType ArchiveType { get; set; }
        public UnarchiveProgress Progress { get; set; }
        public DateTime StartTime { get; set; }
    }
}