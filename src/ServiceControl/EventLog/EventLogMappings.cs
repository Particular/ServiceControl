namespace ServiceControl.EventLog
{
    using System;
    using System.Collections.Generic;
    using ServiceControl.Infrastructure.DomainEvents;

    public class EventLogMappings
    {
        Dictionary<Type, Type> mappings;

        public EventLogMappings(Dictionary<Type, Type> mappings)
        {
            this.mappings = mappings;
        }

        public bool HasMapping(IDomainEvent message)
        {
            return mappings.ContainsKey(message.GetType());
        }

        public EventLogItem ApplyMapping(string messageId, IDomainEvent @event)
        {
            var mapping = (IEventLogMappingDefinition)Activator.CreateInstance(mappings[@event.GetType()]);

            return mapping.Apply(messageId, @event);
        }
    }
}