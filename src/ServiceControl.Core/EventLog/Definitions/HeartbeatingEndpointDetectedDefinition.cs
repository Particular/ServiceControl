namespace ServiceControl.EventLog.Definitions
{
    using Contracts.HeartbeatMonitoring;

    public class HeartbeatingEndpointDetectedDefinition : EventLogMappingDefinition<HeartbeatingEndpointDetected>
    {
        public HeartbeatingEndpointDetectedDefinition()
        {
            Description(m => string.Format("Endpoint {0} running on host {1} has been confirmed to have heartbeats enabled", m.Endpoint.Name, m.Endpoint.Host));

            RelatesToEndpoint(m => m.Endpoint.Host);
            RelatesToHost(m => m.Endpoint.HostId);

            RaisedAt(m => m.DetectedAt);
        }
    }
}