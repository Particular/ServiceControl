namespace ServiceControl.EventLog
{
    using System;
    using System.Collections.Generic;
    using Infrastructure.DomainEvents;

    public class EventLogMappings
    {
        public EventLogMappings(Dictionary<Type, Type> mappings)
        {
            this.mappings = mappings;
        }

        public bool HasMapping(IDomainEvent message)
        {
            return mappings.ContainsKey(message.GetType());
        }

        public EventLogItem ApplyMapping(IDomainEvent @event)
        {
            var mapping = (IEventLogMappingDefinition)Activator.CreateInstance(mappings[@event.GetType()]);

            return mapping.Apply(@event);
        }

        Dictionary<Type, Type> mappings;
    }
}