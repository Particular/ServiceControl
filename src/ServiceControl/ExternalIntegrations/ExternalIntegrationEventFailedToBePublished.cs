namespace ServiceControl.ExternalIntegrations
{
    using System;
    using NServiceBus;

    public class ExternalIntegrationEventFailedToBePublished : IEvent
    {
        public Type EventType { get; set; }
        public string Reason { get; set; }
    }
}