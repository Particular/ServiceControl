namespace ServiceControl.Contracts.Alerts
{
    using System;
    using NServiceBus;
    public class EndpointAlertRaised : IEvent
    {
        public Guid AlertId { get; set; }
        public DateTime AlertReceivedAt { get; set; }
        //TODO: Do we need endpoint instance Id? Who sets the Endpoint instance Id? 
        public string Endpoint { get; set; }
        public string MachineName { get; set; }
    }
}
