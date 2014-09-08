namespace ServiceControl.EventLog
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using NServiceBus;
    using NServiceBus.Configuration.AdvanceExtensibility;

    public class EventLogMappings : INeedInitialization
    {
        Dictionary<Type, Type> mappings = new Dictionary<Type, Type>();

        public bool HasMapping(IEvent message)
        {
            return mappings.ContainsKey(message.GetType());
        }

        public EventLogItem ApplyMapping(IEvent @event, IDictionary<string, string> headers)
        {
            var mapping = (IEventLogMappingDefinition)Activator.CreateInstance(mappings[@event.GetType()]);

            return mapping.Apply(@event, headers);
        }

        public void Customize(BusConfiguration configuration)
        {
            configuration.RegisterComponents(c =>
            {
                var temp = new EventLogMappings();

                foreach (var mapperType in configuration.GetSettings().GetAvailableTypes()
                    .Where(t => typeof(IEventLogMappingDefinition).IsAssignableFrom(t) && !t.IsAbstract && !t.IsInterface))
                {
                    if (mapperType.BaseType == null)
                    {
                        return;
                    }

                    var args = mapperType.BaseType.GetGenericArguments();
                    if (args.Count() == 1)
                    {
                        temp.mappings.Add(args.Single(), mapperType);
                    }

                }

                c.RegisterSingleton(temp);
            });
        }
    }
}