namespace ServiceControl.CompositeViews.Endpoints
{
    using System;
    using EndpointControl;
    using EndpointControl.Contracts;
    using NServiceBus;
    using Raven.Client;

    public class KnownEndpointUpdateHandler : IHandleMessages<EnableEndpointMonitoring>, IHandleMessages<DisableEndpointMonitoring>
    {
        private readonly IBus bus;
        private readonly IDocumentStore store;

        public KnownEndpointUpdateHandler(IBus bus, IDocumentStore store)
        {
            this.bus = bus;
            this.store = store;
        }

        public void Handle(EnableEndpointMonitoring message)
        {
            KnownEndpoint knownEndpoint;

            using (var session = store.OpenSession())
            {
                knownEndpoint = session.Load<KnownEndpoint>(message.EndpointId);

                if (knownEndpoint == null)
                {
                    throw new Exception($"No endpoint with found with id: {message.EndpointId}");
                }

                knownEndpoint.Monitored = true;

                session.SaveChanges();
            }

            bus.Publish(new MonitoringEnabledForEndpoint
            {
                EndpointInstanceId = message.EndpointId,
                Endpoint = knownEndpoint.EndpointDetails
            });
        }

        public void Handle(DisableEndpointMonitoring message)
        {
            KnownEndpoint knownEndpoint;

            using (var session = store.OpenSession())
            {
                knownEndpoint = session.Load<KnownEndpoint>(message.EndpointId);

                if (knownEndpoint == null)
                {
                    throw new Exception($"No endpoint with found with id: {message.EndpointId}");
                }

                knownEndpoint.Monitored = false;

                session.SaveChanges();
            }

            bus.Publish(new MonitoringDisabledForEndpoint
            {
                EndpointInstanceId = message.EndpointId,
                Endpoint = knownEndpoint.EndpointDetails
            });
        }
    }
}