namespace ServiceControl.Alerts
{
    using System;
    using Contracts.Alerts;
    using Contracts.HeartbeatMonitoring;
    using NServiceBus;

    class EndpointFailedHeartbeatHandler : IHandleMessages<EndpointFailedToHeartbeat>
    {
        public IBus Bus { get; set; }
        public void Handle(EndpointFailedToHeartbeat message)
        {
            // TODO: Store this alert in Raven.

            Bus.Publish<HeartbeatFailedAlert>(m =>
            {
                m.Id = Guid.NewGuid().ToString(); //TODO: Pass in the Raven generated Id for the doc, instead of a new guid.
                m.RaisedAt = DateTime.Now;
                m.Endpoint = message.Endpoint;
                m.Machine = message.Machine;
                m.LastHeartbeatReceivedAt = message.LastReceivedAt;
            });
        }
    }
}
