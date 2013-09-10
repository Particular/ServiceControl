namespace ServiceControl.Contracts.Alerts
{
    using System;
    using NServiceBus;
    using ServiceControl.Alerts;

    public class AlertRaised : IEvent
    {
        public string Id { get; set; }
        public string Description { get; set; }
        public Severity Severity { get; set; }
        public DateTime RaisedAt { get; set; }
        public string Endpoint { get; set; }
        public string Machine { get; set; }
        public string Type { get; set; }
        public string RelatedId { get; set; } // This could be the Id of a related document, such as the FailedMessage event, which will have more information regarding this alert.
    }
}
