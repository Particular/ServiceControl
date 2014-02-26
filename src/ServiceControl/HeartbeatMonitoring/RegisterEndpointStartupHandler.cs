namespace ServiceControl.HeartbeatMonitoring
{
    using Contracts.HeartbeatMonitoring;
    using NServiceBus;
    using Plugin.Heartbeat.Messages;
    using Raven.Client;

    class RegisterEndpointStartupHandler : IHandleMessages<RegisterEndpointStartup>
    {
        public IDocumentSession Session { get; set; }
        public IBus Bus { get; set; }

        public void Handle(RegisterEndpointStartup message)
        {
            Bus.Publish<EndpointStarted>(e =>
            {
                e.HostId = message.HostId;
                e.Endpoint = message.Endpoint;
                e.StartedAt = message.StartedAt;
            });
        }
    }
}