﻿namespace ServiceControl.EventLog.Definitions
{
    using Contracts.HeartbeatMonitoring;

    class HeartbeatingEndpointDetectedDefinition : EventLogMappingDefinition<HeartbeatingEndpointDetected>
    {
        public HeartbeatingEndpointDetectedDefinition()
        {
            Description(m => $"Endpoint {m.Endpoint.Name} running on host {m.Endpoint.Host} has been confirmed to have heartbeats enabled");

            RelatesToEndpoint(m => m.Endpoint.Host);
            RelatesToHost(m => m.Endpoint.HostId);

            RaisedAt(m => m.DetectedAt);
        }
    }
}