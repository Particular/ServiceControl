namespace ServiceControl.EventLog
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using NServiceBus;

    public class EventLogMappings : INeedInitialization
    {
        Dictionary<Type, Type> mappings = new Dictionary<Type, Type>();

        public bool HasMapping(IEvent message)
        {
            return mappings.ContainsKey(message.GetType());
        }

        public EventLogItem ApplyMapping(IEvent @event)
        {
            var mapping = (IEventLogMappingDefinition)Activator.CreateInstance(mappings[@event.GetType()]);

            return mapping.Apply(@event);
        }

        public void Init()
        {
            var temp = new EventLogMappings();

            Configure.Instance.ForAllTypes<IEventLogMappingDefinition>(t =>
            {
                if (t.BaseType == null)
                {
                    return;
                }

                var args = t.BaseType.GetGenericArguments();
                if (args.Count() == 1)
                {
                    temp.mappings.Add(args.Single(), t);
                }
            });

            Configure.Instance.Configurer.RegisterSingleton<EventLogMappings>(temp);
        }
    }
}