namespace ServiceControl.EventLog.Definitions
{
    using Contracts.HeartbeatMonitoring;

    public class HeartbeatingEndpointDetectedDefinition : EventLogMappingDefinition<HeartbeatingEndpointDetected>
    {
        public HeartbeatingEndpointDetectedDefinition()
        {
            Description(m => string.Format("Endpoint {0} running on host {1} has been confirmed to have heartbeats enabled", m.EndpointDetails.Name, m.EndpointDetails.Host));

            RelatesToEndpoint(m => m.EndpointDetails.Host);
            RelatesToHost(m => m.EndpointDetails.HostId);

            RaisedAt(m => m.DetectedAt);
        }
    }
}