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
#pragma warning disable RS0030 // Do not use banned apis: default field value does not need to use external time provider
            ReportedAt = DateTime.UtcNow;
#pragma warning restore RS0030
        }

        public required EndpointDetails OriginatingEndpoint { get; set; }
        public required string CustomCheckId { get; set; }
        public DateTime ReportedAt { get; set; }
        public required string Category { get; set; }
        public bool HasFailed { get; set; }
        public string? FailureReason { get; set; }

        public Guid GetDeterministicId() => DeterministicGuid.MakeId(OriginatingEndpoint.Name ?? "", OriginatingEndpoint.HostId.ToString(), CustomCheckId);
    }
}