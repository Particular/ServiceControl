namespace ServiceControl.EventLog.Definitions
{
    using EndpointControl;

    public class KnownEndpointUpdatedDefinition : EventLogMappingDefinition<KnownEndpointUpdated>
    {
        public KnownEndpointUpdatedDefinition()
        {
            Description(m => "Endpoint configuration updated.");

            RelatesToEndpoint(m => m.Name);
            RelatesToHost(m => m.HostId);
        }

        public override string Category
        {
            get { return "Endpoints"; }
        }
    }
}