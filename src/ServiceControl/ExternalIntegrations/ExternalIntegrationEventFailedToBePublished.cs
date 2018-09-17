namespace ServiceControl.ExternalIntegrations
{
    using System;
    using Infrastructure.DomainEvents;

    class ExternalIntegrationEventFailedToBePublished : IDomainEvent
    {
        public Type EventType { get; set; }
        public string Reason { get; set; }
    }
}