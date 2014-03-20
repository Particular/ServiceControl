namespace ServiceControl.CompositeViews.Endpoints
{
    using System;
    using EndpointControl;
    using EndpointControl.Contracts;
    using NServiceBus;
    using Raven.Client;

    public class KnownEndpointUpdateHandler : IHandleMessages<EnableEndpointMonitoring>, IHandleMessages<DisableEndpointMonitoring>
    {
        public IDocumentSession Session { get; set; }

        public IBus Bus { get; set; }

        public void Handle(EnableEndpointMonitoring message)
        {
            var knownEndpoint = Session.Load<KnownEndpoint>(message.EndpointId);

            if (knownEndpoint == null)
            {
                throw new Exception("No endpoint with found with id: " + message.EndpointId);
            }

            knownEndpoint.MonitorHeartbeat = true;

            Session.Store(knownEndpoint);

            Bus.Publish(new MonitoringEnabledForEndpoint
            {
                EndpointId = message.EndpointId,
                Endpoint = knownEndpoint.EndpointDetails
            });
        }

        public void Handle(DisableEndpointMonitoring message)
        {
            var knownEndpoint = Session.Load<KnownEndpoint>(message.EndpointId);

            if (knownEndpoint == null)
            {
                throw new Exception("No endpoint with found with id: " + message.EndpointId);
            }

            knownEndpoint.MonitorHeartbeat = false;

            Session.Store(knownEndpoint);

            Bus.Publish(new MonitoringDisabledForEndpoint
            {
                EndpointId = message.EndpointId,
                Endpoint = knownEndpoint.EndpointDetails
            });
        }
    }
}