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
        EndpointInstanceMonitoring monitoring;
        IDomainEvents domainEvents;

        public RegisterEndpointStartupHandler(EndpointInstanceMonitoring monitoring, IDomainEvents domainEvents)
        {
            this.monitoring = monitoring;
            this.domainEvents = domainEvents;
        }

        public void Handle(RegisterEndpointStartup message)
        {
            monitoring.GetOrCreateMonitor(
                new EndpointInstanceId(message.Endpoint, message.Host, message.HostId),
                true
            );

            domainEvents.Raise(new EndpointStarted
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