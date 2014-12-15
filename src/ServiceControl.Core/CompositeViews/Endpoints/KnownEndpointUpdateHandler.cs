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

            knownEndpoint.Monitored = true;

            Session.Store(knownEndpoint);

            Bus.Publish(new MonitoringEnabledForEndpoint
            {
                EndpointInstanceId = message.EndpointId,
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

            knownEndpoint.Monitored = false;

            Session.Store(knownEndpoint);

            Bus.Publish(new MonitoringDisabledForEndpoint
            {
                EndpointInstanceId = message.EndpointId,
                Endpoint = knownEndpoint.EndpointDetails
            });
        }
    }
}