namespace ServiceControl.CompositeViews.Endpoints
{
    using EndpointControl;
    using NServiceBus;
    using Raven.Client;

    public class KnownEndpointUpdateHandler : IHandleMessages<KnownEndpointUpdate>
    {
        public IDocumentSession Session { get; set; }

        public IBus Bus { get; set; }

        public void Handle(KnownEndpointUpdate message)
        {
            var knownEndpoint = Session.Load<KnownEndpoint>(message.KnownEndpointId);

            knownEndpoint.MonitorHeartbeat = message.MonitorHeartbeat;

            Session.Store(knownEndpoint);

            Bus.Publish(new KnownEndpointUpdated
            {
                KnownEndpointId = message.KnownEndpointId,
                Name = knownEndpoint.Name,
                HostDisplayName = knownEndpoint.HostDisplayName,
                HostId = knownEndpoint.HostId,
            });
        }
    }
}