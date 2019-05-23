﻿namespace ServiceControl.EventLog.Definitions
{
    using EndpointControl.Contracts;

    class KnownEndpointUpdatedDefinition : EventLogMappingDefinition<MonitoringEnabledForEndpoint>
    {
        public KnownEndpointUpdatedDefinition()
        {
            Description(m => "Endpoint configuration updated.");

            RelatesToEndpoint(m => m.Endpoint.Name);
        }

        public override string Category => "Endpoints";
    }
}