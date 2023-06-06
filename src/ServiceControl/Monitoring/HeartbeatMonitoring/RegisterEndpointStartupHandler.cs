namespace ServiceControl.HeartbeatMonitoring
{
    using System.Threading.Tasks;
    using NServiceBus;
    using Plugin.Heartbeat.Messages;
    using ServiceControl.Operations;
    using ServiceControl.Persistence;

    class RegisterEndpointStartupHandler : IHandleMessages<RegisterEndpointStartup>
    {
        public RegisterEndpointStartupHandler(IEndpointInstanceMonitoring monitoring)
        {
            this.monitoring = monitoring;
        }

        public Task Handle(RegisterEndpointStartup message, IMessageHandlerContext context)
        {
            var endpointDetails = new EndpointDetails
            {
                Host = message.Host,
                HostId = message.HostId,
                Name = message.Endpoint
            };
            return monitoring.DetectEndpointFromHeartbeatStartup(endpointDetails, message.StartedAt);
        }

        IEndpointInstanceMonitoring monitoring;
    }
}