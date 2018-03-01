namespace ServiceControl.ExternalIntegrations
{
    using System;
    using ServiceControl.Infrastructure.DomainEvents;

    public class ExternalIntegrationEventFailedToBePublished : IDomainEvent
    {
        public Type EventType { get; set; }
        public string Reason { get; set; }
    }
}