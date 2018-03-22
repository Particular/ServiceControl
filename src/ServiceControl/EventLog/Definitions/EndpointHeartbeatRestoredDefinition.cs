namespace ServiceControl.EventLog.Definitions
{
    using Particular.HealthMonitoring.Uptime.Api;

    public class EndpointHeartbeatRestoredDefinition : EventLogMappingDefinition<EndpointHeartbeatRestored>
    {
        public EndpointHeartbeatRestoredDefinition()
        {
            Description(m => "Endpoint heartbeats have been restored.");

            RelatesToEndpoint(m => m.Endpoint.Name);
            RelatesToHost(m => m.Endpoint.HostId);

            RaisedAt(m => m.RestoredAt);
        }
    }
}
