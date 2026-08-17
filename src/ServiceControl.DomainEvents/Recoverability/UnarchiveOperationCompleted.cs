namespace ServiceControl.Recoverability
{
    using System;
    using Infrastructure.DomainEvents;

    public class UnarchiveOperationCompleted : IDomainEvent
    {
        public string RequestId { get; set; }
        public string GroupName { get; set; }
        public ArchiveType ArchiveType { get; set; }
        public UnarchiveProgress Progress { get; set; }
        public DateTime StartTime { get; set; }
        public DateTime Last { get; set; }
        public DateTime CompletionTime { get; set; }
    }
}