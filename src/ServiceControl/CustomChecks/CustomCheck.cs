namespace ServiceControl.CustomChecks
{
    using System;

    class CustomCheck
    {
        public string Id { get; set; }
        public string Category { get; set; }
        public Status Status { get; set; }
        public DateTime ReportedAt { get; set; }
        public string FailureReason { get; set; }
    }
}