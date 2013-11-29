namespace ServiceControl.EventLog
{
    using System;
    using NServiceBus;


    public interface IEventLogMappingDefinition
    {
        Type GetEventType();
        Func<IEvent, EventLogItem> RetrieveMapping();
    }
}
