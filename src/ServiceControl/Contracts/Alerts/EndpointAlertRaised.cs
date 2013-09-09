namespace ServiceControl.Contracts.Alerts
{
    using System;
    using NServiceBus;
    public class EndpointAlertRaised : IEvent
    {
        public string Id { get; set; }
        public DateTime RaisedAt { get; set; }
        public string Endpoint { get; set; }
        public string Machine { get; set; }
        // TODO: Do we need a type since we have explicit events?
        // public string Type { get; set; }
    }
}
