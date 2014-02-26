namespace ServiceControl.EventLog.Definitions
{
    using Contracts.HeartbeatMonitoring;

    public class HeartbeatingEndpointDetectedDefinition : EventLogMappingDefinition<HeartbeatingEndpointDetected>
    {
        public HeartbeatingEndpointDetectedDefinition()
        {
            Description(m => string.Format("Endpoint {0} running on machine {1} has been confirmed to have heartbeats enabled", m.Endpoint, m.HostId));

            RelatesToEndpoint(m => m.Endpoint);
            RelatesToHost(m => m.HostId.ToString());

            RaisedAt(m => m.DetectedAt);
        }
    }
}