namespace ServiceControl.Monitoring.EventLog
{
    using Contracts.EndpointControl;
    using ServiceControl.EventLog;

    class EndpointStartedDefinition : EventLogMappingDefinition<EndpointStarted>
    {
        public EndpointStartedDefinition()
        {
            Description(m => $"Endpoint '{m.EndpointDetails.Name}' started on host {m.EndpointDetails.Host}");

            RelatesToEndpoint(m => m.EndpointDetails.Name);
            RelatesToHost(m => m.EndpointDetails.HostId);

            RaisedAt(m => m.StartedAt);
        }
    }
}