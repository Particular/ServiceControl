namespace ServiceControl.Api.Contracts
{
    using System;

    public class PlatformHealthView
    {
        public string Status { get; set; }
        public string Severity { get; set; }
        public PlatformHealthAlert[] Alerts { get; set; }
    }

    public class PlatformHealthAlert
    {
        public Guid Id { get; set; }
        public string CheckId { get; set; }
        public string Category { get; set; }
        public string Message { get; set; }
        public DateTime ReportedAt { get; set; }
        public string InstanceName { get; set; }
        public string Host { get; set; }
        public Guid HostId { get; set; }
    }
}