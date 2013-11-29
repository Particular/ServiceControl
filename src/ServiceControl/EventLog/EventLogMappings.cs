namespace ServiceControl.EventLog
{
    using System;
    using System.Collections.Generic;
    using NServiceBus;

    public class EventLogMappings : INeedInitialization
    {
        Dictionary<Type, Func<IEvent, EventLogItem>> mappings = new Dictionary<Type, Func<IEvent, EventLogItem>>();

        public bool HasMapping(IEvent message)
        {
            return mappings.ContainsKey(message.GetType());
        }

        public EventLogItem ApplyMapping(IEvent @event)
        {
            return mappings[@event.GetType()](@event);
        }

        public void Init()
        {
            var temp = new EventLogMappings();

            Configure.Instance.ForAllTypes<IEventLogMappingDefinition>(t =>
                {
                    var definition = (IEventLogMappingDefinition)(Activator.CreateInstance(t));
                    temp.mappings.Add(definition.GetEventType(), definition.RetrieveMapping());
                });

            Configure.Instance.Configurer.RegisterSingleton<EventLogMappings>(temp);
        }
    }
}