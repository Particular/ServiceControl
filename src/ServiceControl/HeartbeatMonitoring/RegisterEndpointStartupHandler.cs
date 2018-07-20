namespace ServiceControl.HeartbeatMonitoring
{
    using System.Threading.Tasks;
    using Contracts.Operations;
    using Monitoring;
    using NServiceBus;
    using Plugin.Heartbeat.Messages;

    class RegisterEndpointStartupHandler : IHandleMessages<RegisterEndpointStartup>
    {
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

        EndpointInstanceMonitoring monitoring;
    }
}