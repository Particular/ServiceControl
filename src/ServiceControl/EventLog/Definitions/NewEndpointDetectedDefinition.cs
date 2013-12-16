namespace ServiceControl.EventLog.Definitions
{
    using Contracts.EndpointControl;

    public class NewEndpointDetectedDefinition : EventLogMappingDefinition<NewEndpointDetected>
    {
        public NewEndpointDetectedDefinition()
        {
            Description(m => "New endpoint detected - " + m.Endpoint);

            RelatesToEndpoint(m => m.Endpoint);
            RelatesToMachine(m => m.Machine);

            RaisedAt(m => m.DetectedAt);
        }
    }
}