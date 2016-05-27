namespace ServiceControl.EventLog.Definitions
{
    using Contracts.EndpointControl;

    public class NewEndpointDetectedDefinition : EventLogMappingDefinition<NewEndpointDetected>
    {
        public NewEndpointDetectedDefinition()
        {
            Description(m => $"New  '{m.Endpoint.Name}' endpoint detected at '{m.Endpoint.Host}'. In order for this endpoint to be monitored the plugin needs to be installed.");

            RelatesToEndpoint(m => m.Endpoint.Name);
            RelatesToHost(m => m.Endpoint.HostId);

            RaisedAt(m => m.DetectedAt);
        }
    }
}