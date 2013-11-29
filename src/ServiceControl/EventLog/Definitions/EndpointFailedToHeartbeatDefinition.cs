namespace ServiceControl.EventLog.Definitions
{
    using System;
    using System.Collections.Generic;
    using Contracts.HeartbeatMonitoring;

    public class EndpointFailedToHeartbeatDefinition : EventLogMappingDefinition<EndpointFailedToHeartbeat>
    {
        public override Func<EndpointFailedToHeartbeat, EventLogItem> GetMapping()
        {
            return m => new EventLogItem()
            {
                Description = "Endpoint has failed to send expected heartbeat to ServiceControl. It is possible that the endpoint could be down or is unresponsive. If this condition persists, you might want to restart your endpoint.",
                RelatedTo = new List<string>() { string.Format("endpoint/{0}/{1}", m.Endpoint, m.Machine) },
                Severity = Severity.Error,
                RaisedAt = m.LastReceivedAt,
                Category = "Heartbeats"
            };
        }
    }
}
