namespace ServiceControl.EventLog
{
    using System.Collections.Generic;
    using NServiceBus;


    public interface IEventLogMappingDefinition
    {
        EventLogItem Apply(IEvent @event, IDictionary<string, string> headers);
    }
}
