namespace ServiceControl.EventLog.Definitions
{
    using Contracts.EndpointControl;

    public class NewMachineDetectedForEndpointDefinition : EventLogMappingDefinition<NewMachineDetectedForEndpoint>
    {
        public NewMachineDetectedForEndpointDefinition()
        {
            Description(m => string.Format("New machine: {0} detected for endpoint - {1}" ,m.Machine, m.Endpoint));

            RelatesToEndpoint(m => m.Endpoint);
            RelatesToMachine(m => m.Machine);

            RaisedAt(m => m.DetectedAt);
        }
    }
}