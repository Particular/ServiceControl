namespace ServiceControl.Monitoring.EventLog
{
    using Contracts.HeartbeatMonitoring;
    using ServiceControl.EventLog;

    class EndpointHeartbeatRestoredDefinition : EventLogMappingDefinition<EndpointHeartbeatRestored>
    {
        public EndpointHeartbeatRestoredDefinition()
        {
            Description(m => $"Endpoint {m.Endpoint.Name} heartbeats have been restored.");

            RelatesToEndpoint(m => m.Endpoint.Name);
            RelatesToHost(m => m.Endpoint.HostId);

            RaisedAt(m => m.RestoredAt);
        }
    }
}