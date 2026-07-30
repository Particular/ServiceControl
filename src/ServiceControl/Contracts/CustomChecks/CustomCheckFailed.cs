namespace ServiceControl.Contracts.CustomChecks
{
    using System;
    using Infrastructure.DomainEvents;
    using ServiceControl.Operations;

    public class CustomCheckFailed : IDomainEvent
    {
        public string CustomCheckId { get; set; }
        public string Category { get; set; }
        public string FailureReason { get; set; }
        public DateTime FailedAt { get; set; }
        public EndpointDetails OriginatingEndpoint { get; set; }
        public Guid Id { get; set; }
    }
}