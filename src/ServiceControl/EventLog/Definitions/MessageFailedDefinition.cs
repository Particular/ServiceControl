
namespace ServiceControl.EventLog.Definitions
{
    using System;
    using System.Collections.Generic;
    using Contracts.MessageFailures;

    public class MessageFailedDefinition : EventLogMappingDefinition<MessageFailed>
    {
        public override Func<MessageFailed, EventLogItem> GetMapping()
        {
            return m => new EventLogItem()
            {
                Description = m.Reason,
                RelatedTo = new List<string>() { string.Format("/failedMessageId/{0}", m.Id), string.Format("/endpoint/{0}/{1}", m.Endpoint, m.Machine) },
                Severity = Severity.Error,
                RaisedAt = m.FailedAt,
                Category = "MessageFailures"
            };
        }
    }
}
