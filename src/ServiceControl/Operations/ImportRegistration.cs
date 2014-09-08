namespace ServiceControl.Operations
{
    using System.Linq;
    using NServiceBus;
    using NServiceBus.Configuration.AdvanceExtensibility;

    class ImportRegistration : INeedInitialization
    {
        public void Customize(BusConfiguration configuration)
        {
            configuration.RegisterComponents(c =>
            {
                foreach (var enricherType in configuration.GetSettings().GetAvailableTypes()
                    .Where(t => typeof(IEnrichImportedMessages).IsAssignableFrom(t) && !t.IsAbstract && !t.IsInterface))
                {
                    c.ConfigureComponent(enricherType, DependencyLifecycle.SingleInstance);
                }
            });
        }
    }
}