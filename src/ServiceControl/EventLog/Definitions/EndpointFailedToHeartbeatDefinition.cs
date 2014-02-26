namespace ServiceControl.EventLog.Definitions
{
    using Contracts.HeartbeatMonitoring;

    public class EndpointFailedToHeartbeatDefinition : EventLogMappingDefinition<EndpointFailedToHeartbeat>
    {
        public EndpointFailedToHeartbeatDefinition()
        {
            TreatAsError();

            Description(m => "Endpoint has failed to send expected heartbeat to ServiceControl. It is possible that the endpoint could be down or is unresponsive. If this condition persists, you might want to restart your endpoint.");

            RelatesToEndpoint(m => m.Endpoint);
            RelatesToHost(m => m.HostId.ToString());

            RaisedAt(m => m.LastReceivedAt);
        }
    }
}
