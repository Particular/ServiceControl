namespace Particular.HealthMonitoring.Uptime
{
    using System.Threading.Tasks;
    using Particular.Operations.Heartbeats.Api;
    using ServiceControl.Monitoring;

    public class HeartbeatProcessor : IProcessHeartbeats
    {
        EndpointInstanceMonitoring monitoring;

        public HeartbeatProcessor(EndpointInstanceMonitoring monitoring)
        {
            this.monitoring = monitoring;
        }

        public Task Handle(RegisterEndpointStartup endpointStartup)
        {
            return Task.FromResult(0);
        }

        public Task Handle(EndpointHeartbeat heartbeat)
        {
            monitoring.RecordHeartbeat(heartbeat.EndpointName, heartbeat.Host, heartbeat.HostId, heartbeat.ExecutedAt);
            return Task.FromResult(0);
        }
    }
}