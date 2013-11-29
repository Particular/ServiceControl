namespace ServiceControl.EventLog.Definitions
{
    using System;
    using System.Collections.Generic;
    using Contracts.HeartbeatMonitoring;

    public class EndpointHeartbeatRestoredDefinition : EventLogMappingDefinition<EndpointHeartbeatRestored>
    {
        public override Func<EndpointHeartbeatRestored, EventLogItem> GetMapping()
        {
            return m => new EventLogItem()
            {
                Description = "Endpoint heartbeats have been restored.",
                RelatedTo = new List<string>() { string.Format("endpoint/{0}/{1}", m.Endpoint, m.Machine) },
                Severity = Severity.Info,
                RaisedAt = m.RestoredAt,
                Category = "Heartbeats"
            };
        }
    }
}
