namespace ServiceControl.EventLog.Definitions
{
    using System;
    using System.Collections.Generic;
    using Contracts.CustomChecks;

    public class CustomCheckSucceededDefinition : EventLogMappingDefinition<CustomCheckSucceeded>
    {
        public override Func<CustomCheckSucceeded, EventLogItem> GetMapping()
        {
            return m => new EventLogItem()
            {
                Description = String.Format("{0}: Working as expected.", m.CustomCheckId),
                RelatedTo = new List<string> { String.Format("endpoint/{0}/{1}", m.OriginatingEndpoint.Name, m.OriginatingEndpoint.Machine) },
                Severity = Severity.Info,
                RaisedAt = m.SucceededAt,
                Category = m.Category
            };
        }
    }
}
