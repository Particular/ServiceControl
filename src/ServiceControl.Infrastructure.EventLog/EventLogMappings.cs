namespace ServiceControl.EventLog
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using Infrastructure.DomainEvents;

    class EventLogMappings
    {
        public EventLogMappings(IEnumerable<IEventLogMappingDefinition> mappers)
        {
            foreach (var mapper in mappers)
            {
                var type = mapper.GetType();
                if (type.BaseType == null)
                {
                    continue;
                }

                var args = type.BaseType.GetGenericArguments();
                if (args.Length == 1)
                {
                    mappings.Add(args.Single(), mapper);
                }
            }
        }

        public bool HasMapping(IDomainEvent message)
        {
            return mappings.ContainsKey(message.GetType());
        }

        public EventLogItem ApplyMapping(IDomainEvent @event)
        {
            var mapping = mappings[@event.GetType()];
            return mapping.Apply(@event);
        }

        Dictionary<Type, IEventLogMappingDefinition> mappings = new Dictionary<Type, IEventLogMappingDefinition>();
    }
}