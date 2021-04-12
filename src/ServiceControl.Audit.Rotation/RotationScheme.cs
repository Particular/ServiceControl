// unset

namespace ServiceControl.Audit.Rotation
{
    using System;
    using System.Collections.Generic;

    public class RotationScheme
    {
        public string AuditQueue { get; set; }
        public List<string> Instances { get; set; }
        public TimeSpan? TimerTrigger { get; set; }
        public int? SizeTriggerMB { get; set; }
    }
}