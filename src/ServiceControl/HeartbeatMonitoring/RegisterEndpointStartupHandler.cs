namespace ServiceControl.HeartbeatMonitoring
{
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

        public void Handle(RegisterEndpointStartup message)
        {
            var endpointDetails = new EndpointDetails
            {
                Host = message.Host,
                HostId = message.HostId,
                Name = message.Endpoint
            };
            monitoring.DetectEndpointFromHeartbeatStartup(endpointDetails, message.StartedAt);
        }
    }
}