namespace ServiceControl.EventLog.Definitions
{
    using EndpointControl;

    public class KnownEndpointUpdatedDefinition : EventLogMappingDefinition<KnownEndpointUpdated>
    {
        public KnownEndpointUpdatedDefinition()
        {
            Description(m => "Endpoint configuration updated.");

            RelatesToEndpoint(m => m.Name);
            RelatesToMachine(m => m.HostDisplayName);
        }

        public override string Category
        {
            get { return "Endpoints"; }
        }
    }
}