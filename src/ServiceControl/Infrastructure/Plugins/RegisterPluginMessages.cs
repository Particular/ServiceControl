namespace ServiceControl.Infrastructure.Plugins
{
    using NServiceBus;
    using NServiceBus.Configuration.AdvanceExtensibility;

    class RegisterPluginMessages : INeedInitialization
    {
        public void Customize(BusConfiguration configuration)
        {
            configuration.GetSettings().Get<Conventions>().AddSystemMessagesConventions(t => t.Namespace != null
                && t.Namespace.StartsWith("ServiceControl.Plugin.")
                && t.Namespace.EndsWith(".Messages"));
        }
    }
}
