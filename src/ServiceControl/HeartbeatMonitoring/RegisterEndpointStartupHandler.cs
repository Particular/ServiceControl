namespace ServiceControl.HeartbeatMonitoring
{
    using Contracts.EndpointControl;
    using Contracts.Operations;
    using NServiceBus;
    using Plugin.Heartbeat.Messages;

    class RegisterEndpointStartupHandler : IHandleMessages<RegisterEndpointStartup>
    {
        public IBus Bus { get; set; }

        public void Handle(RegisterEndpointStartup message)
        {
            Bus.Publish<EndpointStarted>(e =>
            {
                e.EndpointDetails = new EndpointDetails
                {
                    Host = message.Host,
                    HostId = message.HostId,
                    Name = message.Endpoint
                };
                e.StartedAt = message.StartedAt;
            });
        }
    }
}