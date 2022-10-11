namespace ServiceControl.Monitoring.EventLog
{
    using Contracts.HeartbeatMonitoring;
    using ServiceControl.EventLog;

    class EndpointFailedToHeartbeatDefinition : EventLogMappingDefinition<EndpointFailedToHeartbeat>
    {
        public EndpointFailedToHeartbeatDefinition()
        {
            TreatAsError();

            Description(m => $"Endpoint {m.Endpoint.Name} has failed to send expected heartbeat to ServiceControl on host {m.Endpoint.Host}. It is possible that the endpoint could be down or is unresponsive. If this condition persists restart the endpoint.");

            RelatesToEndpoint(m => m.Endpoint.Name);
            RelatesToHost(m => m.Endpoint.HostId);

            RaisedAt(m => m.LastReceivedAt);
        }
    }
}