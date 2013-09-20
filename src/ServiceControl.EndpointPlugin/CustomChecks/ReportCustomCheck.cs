namespace ServiceControl.EndpointPlugin.CustomChecks
{
    using System;

    public class ReportCustomCheck
    {
        public string CustomCheckId { get; set; }
        public string Category { get; set; }
        public CustomCheckResult Result { get; set; }
        public DateTime ReportedAt { get; set; }
    }
}