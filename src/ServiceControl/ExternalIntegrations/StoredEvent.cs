namespace ServiceControl.ExternalIntegrations
{
    using System;
    using NServiceBus;

    public class StoredEvent
    {
        public string Type { get; set; }
        public IEvent Payload { get; set; }
        public bool Dispatched { get; set; }
        public DateTime RegistrationDate { get; set; }
    }
}