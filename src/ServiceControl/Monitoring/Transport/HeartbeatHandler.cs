namespace ServiceControl.Monitoring
{
    using NServiceBus;
    using Particular.HealthMonitoring.Uptime;
    using ServiceControl.Contracts.Operations;
    using ServiceControl.Plugin.Heartbeat.Messages;

    class HeartbeatHandler : IHandleMessages<EndpointHeartbeat>
    {
        public void Handle(EndpointHeartbeat message)
        {
            monitor.RecordHeartbeat(new Particular.Operations.Heartbeats.Api.EndpointHeartbeat
            {
                EndpointName = message.EndpointName,
                ExecutedAt = message.ExecutedAt,
                Host = message.Host,
                HostId = message.HostId,
            });

            persister.RegisterEndpoint(new EndpointDetails
            {
                Host = message.Host,
                HostId = message.HostId,
                Name =  message.EndpointName
            });
        }

        public HeartbeatHandler(UptimeMonitoring uptimeMonitoring, MonitoringDataPersister persister)
        {
            this.persister = persister;
            this.monitor = uptimeMonitoring.Monitoring;
        }

        EndpointInstanceMonitoring monitor;
        MonitoringDataPersister persister;
    }
}