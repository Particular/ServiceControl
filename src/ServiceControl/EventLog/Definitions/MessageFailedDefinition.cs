
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
                Description = m.FailureDetails.Exception.Message,
                RelatedTo = new List<string>() { string.Format("/message/{0}", m.FailedMessageId), string.Format("/endpoint/{0}", m.EndpointId) },
                Severity = Severity.Error,
                RaisedAt = m.FailureDetails.TimeOfFailure,
                Category = "MessageFailures"
            };
        }
    }
}
