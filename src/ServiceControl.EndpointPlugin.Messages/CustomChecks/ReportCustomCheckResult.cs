namespace ServiceControl.EndpointPlugin.Messages.CustomChecks
{
    using System;

    public class ReportCustomCheckResult
    {
        public string CustomCheckId { get; set; }
        public string Category { get; set; }
        public CustomCheckResult Result { get; set; }
        public DateTime ReportedAt { get; set; }
    }
}
