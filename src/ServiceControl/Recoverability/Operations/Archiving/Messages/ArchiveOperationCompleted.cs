﻿namespace ServiceControl.Recoverability
{
    using System;
    using NServiceBus;

    public class ArchiveOperationCompleted : IEvent
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