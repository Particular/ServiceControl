namespace ServiceControl.EventLog.Definitions
{
    using Contracts.HeartbeatMonitoring;


    public class EndpointHeartbeatRestoredDefinition : EventLogMappingDefinition<EndpointHeartbeatRestored>
    {
        public EndpointHeartbeatRestoredDefinition()
        {
            Description(m => "Endpoint heartbeats have been restored.");

            RelatesToEndpoint(m => m.Endpoint);
            RelatesToHost(m => m.HostId);

            RaisedAt(m => m.RestoredAt);
        }
    }
}
