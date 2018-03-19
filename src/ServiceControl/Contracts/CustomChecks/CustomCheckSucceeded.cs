namespace ServiceControl.Contracts.CustomChecks
{
    using System;
    using Operations;
    using ServiceControl.Infrastructure.DomainEvents;

    public class CustomCheckSucceeded : IDomainEvent
    {
        public Guid Id { get; set; }

        public string CustomCheckId { get; set; }
        public string Category { get; set; }
        public DateTime SucceededAt { get; set; }
        public EndpointDetails OriginatingEndpoint { get; set; }

    }
}
