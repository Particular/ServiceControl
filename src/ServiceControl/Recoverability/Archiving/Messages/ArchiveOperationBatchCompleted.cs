namespace ServiceControl.Recoverability
{
    using System;
    using NServiceBus;

    public class ArchiveOperationBatchCompleted : IEvent
    {
        public string RequestId { get; set; }
        public ArchiveType ArchiveType { get; set; }
        public ArchiveProgress Progress { get; set; }
        public DateTime StartTime { get; set; }
        public DateTime Last { get; set; }
    }
}