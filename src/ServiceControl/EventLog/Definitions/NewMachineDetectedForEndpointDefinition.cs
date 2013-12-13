namespace ServiceControl.EventLog.Definitions
{
    using System;
    using System.Collections.Generic;
    using Contracts.EndpointControl;

    public class NewMachineDetectedForEndpointDefinition : EventLogMappingDefinition<NewMachineDetectedForEndpoint>
    {
        public override Func<NewMachineDetectedForEndpoint, EventLogItem> GetMapping()
        {
            return m => new EventLogItem()
            {
                Description = string.Format("New machine: {0} detected for endpoint - {1}" ,m.Machine, m.Endpoint),
                RelatedTo = new List<string>() { string.Format("/endpoint/{0}", m.Endpoint), string.Format("/machine/{0}", m.Machine) },
                Severity = Severity.Info,
                RaisedAt = m.DetectedAt,
                Category = "Endpoints"
            };
        }
    }
}