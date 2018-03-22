namespace Particular.HealthMonitoring.Uptime
{
    using System.Threading.Tasks;
    using Particular.HealthMonitoring.Uptime.Api;
    using Particular.Operations.Heartbeats.Api;
    using ServiceControl.Infrastructure.DomainEvents;

    class HeartbeatProcessor : IProcessHeartbeats
    {
        IDomainEvents domainEvents;
        EndpointInstanceMonitoring monitoring;
        IPersistEndpointUptimeInformation persister;

        public HeartbeatProcessor(EndpointInstanceMonitoring monitoring, IDomainEvents domainEvents, IPersistEndpointUptimeInformation persister)
        {
            this.domainEvents = domainEvents;
            this.persister = persister;
            this.monitoring = monitoring;
        }

        public Task Handle(RegisterEndpointStartup endpointStartup)
        {
            var @event = monitoring.StartTrackingEndpoint(endpointStartup.Endpoint, endpointStartup.Host, endpointStartup.HostId);
            domainEvents.Raise(@event);
            return persister.Store(new[] { @event });
        }

        public Task Handle(EndpointHeartbeat heartbeat)
        {
            monitoring.RecordHeartbeat(heartbeat.EndpointName, heartbeat.Host, heartbeat.HostId, heartbeat.ExecutedAt);
            return Task.FromResult(0);
        }
    }
}