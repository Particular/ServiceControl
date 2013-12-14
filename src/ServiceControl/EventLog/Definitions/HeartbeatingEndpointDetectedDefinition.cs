namespace ServiceControl.EventLog.Definitions
{
    using System;
    using System.Collections.Generic;
    using Contracts.HeartbeatMonitoring;

    public class HeartbeatingEndpointDetectedDefinition : EventLogMappingDefinition<HeartbeatingEndpointDetected>
    {
        public override Func<HeartbeatingEndpointDetected, EventLogItem> GetMapping()
        {
            return m => new EventLogItem()
            {
                Description = string.Format("Endpoint {0} running on machine {1} has been confirmed to have heartbeats enabled" ,m.Endpoint,m.Machine),
                RelatedTo = new List<string> { string.Format("/endpoint/{0}", m.Endpoint), string.Format("/machine/{0}", m.Machine) },
                Severity = Severity.Info,
                RaisedAt = m.DetectedAt,
                Category = "Heartbeats"
            };
        }
    }
}