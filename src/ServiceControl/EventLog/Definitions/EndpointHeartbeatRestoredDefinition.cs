namespace ServiceControl.EventLog.Definitions
{
    using Contracts.HeartbeatMonitoring;

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