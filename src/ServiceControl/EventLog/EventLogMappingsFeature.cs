namespace ServiceControl.EventLog
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using NServiceBus;
    using NServiceBus.Features;

    public class EventLogMappingsFeature : Feature
    {
        public EventLogMappingsFeature()
        {
            EnableByDefault();
        }

        protected override void Setup(FeatureConfigurationContext context)
        {
            var mappings = new Dictionary<Type, Type>();

            var types = context.Settings.GetAvailableTypes().Implementing<IEventLogMappingDefinition>();

            foreach (var type in types)
            {
                if (type.BaseType == null)
                {
                    return;
                }

                var args = type.BaseType.GetGenericArguments();
                if (args.Count() == 1)
                {
                    mappings.Add(args.Single(), type);
                }
            }

            context.Container.RegisterSingleton(new EventLogMappings(mappings));
            context.Container.ConfigureComponent<AuditEventLogWriter>(DependencyLifecycle.SingleInstance);
        }
    }
}