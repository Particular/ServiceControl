namespace ServiceControl.EventLog.Definitions
{
    using Contracts.EndpointControl;

    public class NewEndpointDetectedDefinition : EventLogMappingDefinition<NewEndpointDetected>
    {
        public NewEndpointDetectedDefinition()
        {
            Description(m => string.Format("New  '{0}' endpoint detected at '{1}'. In order for this endpoint to be monitored the plugin needs to be installed.", m.Endpoint.Name, m.Endpoint.Host));

            RelatesToEndpoint(m => m.Endpoint.Name);
            RelatesToHost(m => m.Endpoint.HostId);

            RaisedAt(m => m.DetectedAt);
        }
    }
}