namespace ServiceControl.Plugin.CustomChecks.Messages
{
    using System;

    class ReportCustomCheckResult
    {
        public Guid HostId { get; set; }
        public string CustomCheckId { get; set; }
        public string Category { get; set; }
        public bool HasFailed { get; set; }
        public string FailureReason { get; set; }

        public DateTime ReportedAt { get; set; }
        public string EndpointName { get; set; }
        public string Host { get; set; }
    }
}
