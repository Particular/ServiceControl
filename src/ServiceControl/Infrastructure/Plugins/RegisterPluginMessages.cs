namespace ServiceControl.Infrastructure.Plugins
{
    using NServiceBus;

    class RegisterPluginMessages : IWantToRunBeforeConfiguration
    {
        public void Init()
        {
            Configure.Instance.AddSystemMessagesAs(t => t.Namespace != null
                && t.Namespace.StartsWith("ServiceControl.Plugin.")
                && t.Namespace.EndsWith(".Messages"));
        }
    }
}
