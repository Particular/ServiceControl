namespace ServiceControl.HeartbeatMonitoring
{
    using System.Threading.Tasks;
    using Contracts.EndpointControl;
    using Contracts.Operations;
    using NServiceBus;
    using Plugin.Heartbeat.Messages;
    using Raven.Client;

    class RegisterEndpointStartupHandler : IHandleMessages<RegisterEndpointStartup>
    {
        public IDocumentSession Session { get; set; }

        public Task Handle(RegisterEndpointStartup message, IMessageHandlerContext context)
        {
            return context.Publish<EndpointStarted>(e =>
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