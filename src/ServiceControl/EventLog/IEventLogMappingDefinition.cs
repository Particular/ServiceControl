namespace ServiceControl.EventLog
{
    using Infrastructure.DomainEvents;

    public interface IEventLogMappingDefinition
    {
        EventLogItem Apply(IDomainEvent @event);
    }
}