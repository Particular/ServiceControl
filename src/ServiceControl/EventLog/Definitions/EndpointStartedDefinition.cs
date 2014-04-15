namespace ServiceControl.EventLog.Definitions
{
    using Contracts.EndpointControl;

    public class EndpointStartedDefinition : EventLogMappingDefinition<EndpointStarted>
    {
        public EndpointStartedDefinition()
        {
            Description(m => string.Format("Endpoint '{0}' started at {1} on host {2}", m.EndpointDetails.Name, m.StartedAt, m.EndpointDetails.Host));

            RelatesToEndpoint(m => m.EndpointDetails.Name);
            RelatesToHost(m => m.EndpointDetails.HostId);

            RaisedAt(m => m.StartedAt);
        }
    }
}