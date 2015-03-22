namespace ServiceControl.EventLog.Definitions
{
    using EndpointControl.Contracts;

    public class KnownEndpointUpdatedDefinition : EventLogMappingDefinition<MonitoringEnabledForEndpoint>
    {
        public KnownEndpointUpdatedDefinition()
        {
            Description(m => "Endpoint configuration updated.");

            RelatesToEndpoint(m => m.Endpoint.Name);
        }

        public override string Category
        {
            get { return "Endpoints"; }
        }
    }
}