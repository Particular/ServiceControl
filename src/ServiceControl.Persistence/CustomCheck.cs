namespace ServiceControl.Contracts.CustomChecks
{
    using System;
    using ServiceControl.Operations;
    using ServiceControl.Persistence;

    public class CustomCheck
    {
        public string Id { get; set; }
        public string CustomCheckId { get; set; }
        public string Category { get; set; }
        public Status Status { get; set; }
        public DateTime ReportedAt { get; set; }
        public string FailureReason { get; set; }
        public EndpointDetails OriginatingEndpoint { get; set; }
    }
}