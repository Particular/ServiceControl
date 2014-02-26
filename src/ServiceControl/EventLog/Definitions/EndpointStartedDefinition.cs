namespace ServiceControl.EventLog.Definitions
{
    using Contracts.HeartbeatMonitoring;

    public class EndpointStartedDefinition : EventLogMappingDefinition<EndpointStarted>
    {
        public EndpointStartedDefinition()
        {
            Description(m => string.Format("Endpoint '{0}' started at: {1} Host: {2}", m.Endpoint, m.StartedAt, m.HostId));

            RelatesToEndpoint(m => m.Endpoint);
            RelatesToHost(m => m.HostId.ToString());

            RaisedAt(m => m.StartedAt);
        }
    }
}