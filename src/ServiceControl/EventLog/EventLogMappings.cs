namespace ServiceControl.EventLog
{
    using System;
    using System.Collections.Generic;
    using NServiceBus;

    public class EventLogMappings
    {
        Dictionary<Type, Type> mappings;

        public EventLogMappings(Dictionary<Type, Type> mappings)
        {
            this.mappings = mappings;
        }

        public bool HasMapping(IEvent message)
        {
            return mappings.ContainsKey(message.GetType());
        }

        public EventLogItem ApplyMapping(string messageId, IEvent @event)
        {
            var mapping = (IEventLogMappingDefinition)Activator.CreateInstance(mappings[@event.GetType()]);

            return mapping.Apply(messageId, @event);
        }
    }
}