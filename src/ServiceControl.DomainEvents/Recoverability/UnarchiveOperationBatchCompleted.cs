namespace ServiceControl.Recoverability
{
    using System;
    using Infrastructure.DomainEvents;

    public class UnarchiveOperationBatchCompleted : IDomainEvent
    {
        public string RequestId { get; set; }
        public ArchiveType ArchiveType { get; set; }
        public UnarchiveProgress Progress { get; set; }
        public DateTime StartTime { get; set; }
        public DateTime Last { get; set; }
    }
}