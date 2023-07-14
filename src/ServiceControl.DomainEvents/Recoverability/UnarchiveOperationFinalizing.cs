namespace ServiceControl.Recoverability
{
    using System;
    using Infrastructure.DomainEvents;
    using Infrastructure.SignalR;

    public class UnarchiveOperationFinalizing : IDomainEvent, IUserInterfaceEvent
    {
        public string RequestId { get; set; }
        public ArchiveType ArchiveType { get; set; }
        public UnarchiveProgress Progress { get; set; }
        public DateTime StartTime { get; set; }
        public DateTime Last { get; set; }
    }
}