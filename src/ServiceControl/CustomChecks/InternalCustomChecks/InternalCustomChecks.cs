namespace ServiceControl.CustomChecks
{
    using System.Linq;
    using NServiceBus;
    using NServiceBus.CustomChecks;
    using NServiceBus.Features;
    using NServiceBus.Hosting;

    class InternalCustomChecks : Feature
    {
        protected override void Setup(FeatureConfigurationContext context)
        {
            // Register all of the ICustomCheck instances
            context.Settings.GetAvailableTypes()
                .Where(t => typeof(ICustomCheck).IsAssignableFrom(t) && !(t.IsAbstract || t.IsInterface))
                .ToList()
                .ForEach(t => context.Container.ConfigureComponent(t, DependencyLifecycle.InstancePerCall));

            // Register a startup task with all of the ICustomCheck instances in it
            context.RegisterStartupTask(b => new InternalCustomChecksStartup(
                b.BuildAll<ICustomCheck>().ToList(),
                b.Build<CustomChecksStorage>(),
                b.Build<HostInformation>(),
                context.Settings.EndpointName())
            );
        }
    }
}