namespace ServiceControl.Infrastructure.Plugins
{
    using NServiceBus;

    class RegisterPluginMessages : IWantToRunBeforeConfiguration
    {
        public void Init()
        {
            MessageConventionExtensions.AddSystemMessagesConventions(t => t.Namespace != null
                && t.Namespace.StartsWith("ServiceControl.Plugin.")
                && t.Namespace.EndsWith(".Messages"));
        }
    }
}
