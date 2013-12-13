namespace ServiceControl.EventLog.Definitions
{
    using System;
    using System.Collections.Generic;
    using Contracts.EndpointControl;

    public class NewEndpointDetectedDefinition : EventLogMappingDefinition<NewEndpointDetected>
    {
        public override Func<NewEndpointDetected, EventLogItem> GetMapping()
        {
            return m => new EventLogItem()
            {
                Description = "New endpoint detected - " + m.Endpoint,
                RelatedTo = new List<string>() { string.Format("/endpoint/{0}", m.Endpoint), string.Format("/machine/{0}", m.Machine) },
                Severity = Severity.Info,
                RaisedAt = m.DetectedAt,
                Category = "Endpoints"
            };
        }
    }
}