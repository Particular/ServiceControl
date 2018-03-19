namespace ServiceControl.Contracts.CustomChecks
{
    using System;
    using Operations;
    using ServiceControl.Infrastructure.DomainEvents;
    using ServiceControl.Infrastructure.SignalR;

    public class CustomCheckFailed : IDomainEvent, IUserInterfaceEvent
    {
        public string CustomCheckId { get; set; }
        public string Category { get; set; }
        public string FailureReason { get; set; }
        public DateTime FailedAt { get; set; }
        public EndpointDetails OriginatingEndpoint { get; set; }
        public Guid Id { get; set; }
    }
}
