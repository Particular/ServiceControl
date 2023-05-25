namespace ServiceControl.Contracts.CustomChecks
{
    using System;
    using ServiceControl.Operations;
    using ServiceControl.Persistence.Infrastructure;

    public enum CheckStateChange
    {
        Changed,
        Unchanged
    }

    public class CustomCheckDetail
    {
        public CustomCheckDetail()
        {
            ReportedAt = DateTime.UtcNow;
        }

        public EndpointDetails OriginatingEndpoint { get; set; }
        public string CustomCheckId { get; set; }
        public DateTime ReportedAt { get; set; }
        public string Category { get; set; }
        public bool HasFailed { get; set; }
        public string FailureReason { get; set; }

        public Guid GetDeterministicId() => DeterministicGuid.MakeId(OriginatingEndpoint.Name, OriginatingEndpoint.HostId.ToString(), CustomCheckId);
    }
}