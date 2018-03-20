namespace ServiceControl.EventLog.Definitions
{
    using Particular.HealthMonitoring.Uptime.Api;

    public class KnownEndpointUpdatedDefinition : EventLogMappingDefinition<MonitoringEnabledForEndpoint>
    {
        public KnownEndpointUpdatedDefinition()
        {
            Description(m => "Endpoint configuration updated.");

            RelatesToEndpoint(m => m.Endpoint.Name);
        }

        public override string Category => "Endpoints";
    }
}