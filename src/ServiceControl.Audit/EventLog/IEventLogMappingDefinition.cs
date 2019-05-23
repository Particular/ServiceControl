namespace ServiceControl.EventLog
{
    using Infrastructure.DomainEvents;

    interface IEventLogMappingDefinition
    {
        EventLogItem Apply(IDomainEvent @event);
    }
}