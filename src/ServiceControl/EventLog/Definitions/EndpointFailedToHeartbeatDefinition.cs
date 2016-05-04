namespace ServiceControl.EventLog.Definitions
{
    using Contracts.HeartbeatMonitoring;

    public class EndpointFailedToHeartbeatDefinition : EventLogMappingDefinition<EndpointFailedToHeartbeat>
    {
        public EndpointFailedToHeartbeatDefinition()
        {
            TreatAsError();

            Description(m => "Endpoint has failed to send expected heartbeat to ServiceControl. It is possible that the endpoint could be down or is unresponsive. If this condition persists restart the endpoint.");

            RelatesToEndpoint(m => m.Endpoint.Name);
            RelatesToHost(m => m.Endpoint.HostId);

            RaisedAt(m => m.LastReceivedAt);
        }
    }
}
