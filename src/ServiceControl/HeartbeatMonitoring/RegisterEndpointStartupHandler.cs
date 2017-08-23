namespace ServiceControl.HeartbeatMonitoring
{
    using NServiceBus;
    using Plugin.Heartbeat.Messages;
    using ServiceControl.Contracts.EndpointControl;
    using ServiceControl.Contracts.Operations;
    using ServiceControl.Infrastructure.DomainEvents;
    using ServiceControl.Monitoring;

    class RegisterEndpointStartupHandler : IHandleMessages<RegisterEndpointStartup>
    {
        private EndpointInstanceMonitoring monitoring;

        public RegisterEndpointStartupHandler(EndpointInstanceMonitoring monitoring)
        {
            this.monitoring = monitoring;
        }

        public void Handle(RegisterEndpointStartup message)
        {
            monitoring.GetOrCreateMonitor(
                new EndpointInstanceId(message.Endpoint, message.Host, message.HostId),
                true
            );

            DomainEvents.Raise(new EndpointStarted
            {
                EndpointDetails = new EndpointDetails
                {
                    Host = message.Host,
                    HostId = message.HostId,
                    Name = message.Endpoint
                },
                StartedAt = message.StartedAt
            });
        }
    }
}