namespace ServiceControl.EventLog.Definitions
{
    using System;
    using System.Collections.Generic;
    using Contracts.CustomChecks;

    public class CustomCheckFailedDefinition : EventLogMappingDefinition<CustomCheckFailed>
    {
        public override Func<CustomCheckFailed, EventLogItem> GetMapping()
        {
            return m => new EventLogItem()
            {
                Description = String.Format("{0}: {1}", m.CustomCheckId, m.FailureReason),
                RelatedTo = new List<string> { String.Format("endpoint/{0}/{1}", m.OriginatingEndpoint.Name, m.OriginatingEndpoint.Machine) },
                Severity = Severity.Error,
                RaisedAt = m.FailedAt,
                Category = m.Category
            };
        }
    }
}
