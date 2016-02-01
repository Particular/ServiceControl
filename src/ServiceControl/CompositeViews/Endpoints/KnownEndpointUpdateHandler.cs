namespace ServiceControl.CompositeViews.Endpoints
{
    using System;
    using System.Threading.Tasks;
    using EndpointControl;
    using EndpointControl.Contracts;
    using NServiceBus;
    using Raven.Client;

    public class KnownEndpointUpdateHandler : IHandleMessages<EnableEndpointMonitoring>, IHandleMessages<DisableEndpointMonitoring>
    {
        public IDocumentSession Session { get; set; }

        public Task Handle(EnableEndpointMonitoring message, IMessageHandlerContext context)
        {
            var knownEndpoint = Session.Load<KnownEndpoint>(message.EndpointId);

            if (knownEndpoint == null)
            {
                throw new Exception("No endpoint with found with id: " + message.EndpointId);
            }

            knownEndpoint.Monitored = true;

            Session.Store(knownEndpoint);

            return context.Publish(new MonitoringEnabledForEndpoint
            {
                EndpointInstanceId = message.EndpointId,
                Endpoint = knownEndpoint.EndpointDetails
            });
        }

        public Task Handle(DisableEndpointMonitoring message, IMessageHandlerContext context)
        {
            var knownEndpoint = Session.Load<KnownEndpoint>(message.EndpointId);

            if (knownEndpoint == null)
            {
                throw new Exception("No endpoint with found with id: " + message.EndpointId);
            }

            knownEndpoint.Monitored = false;

            Session.Store(knownEndpoint);

            return context.Publish(new MonitoringDisabledForEndpoint
            {
                EndpointInstanceId = message.EndpointId,
                Endpoint = knownEndpoint.EndpointDetails
            });
        }
    }
}