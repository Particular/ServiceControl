namespace ServiceControl.Monitoring.EventLog
{
    using EndpointControl.Contracts;
    using ServiceControl.EventLog;

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