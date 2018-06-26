namespace ServiceControl.HeartbeatMonitoring
{
    using System.Threading.Tasks;
    using NServiceBus;
    using Plugin.Heartbeat.Messages;
    using ServiceControl.Contracts.Operations;
    using ServiceControl.Monitoring;

    class RegisterEndpointStartupHandler : IHandleMessages<RegisterEndpointStartup>
    {
        EndpointInstanceMonitoring monitoring;

        public RegisterEndpointStartupHandler(EndpointInstanceMonitoring monitoring)
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
    }
}