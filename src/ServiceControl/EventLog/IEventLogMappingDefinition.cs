namespace ServiceControl.EventLog
{
    using NServiceBus;


    public interface IEventLogMappingDefinition
    {
        EventLogItem Apply(string messageId, IEvent @event);
    }
}
