namespace ServiceControl.CustomChecks
{
    using System;
    using Contracts.Operations;
    
    class CustomCheck
    {
        public Guid Id { get; set; }
        public string CustomCheckId { get; set; }
        public string Category { get; set; }
        public Status Status { get; set; }
        public DateTime ReportedAt { get; set; }
        public string FailureReason { get; set; }
        public EndpointDetails OriginatingEndpoint { get; set; }
    }
}