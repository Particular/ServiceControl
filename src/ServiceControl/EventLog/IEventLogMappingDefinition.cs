namespace ServiceControl.EventLog
{
    using NServiceBus;


    public interface IEventLogMappingDefinition
    {
        EventLogItem Apply(IEvent @event);
    }
}
