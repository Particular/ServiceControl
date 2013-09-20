namespace ServiceControl.EndpointPlugin.Operations.ServiceControlBackend
{
    using NServiceBus;

    class InitializeBackend : INeedInitialization
    {
        public void Init()
        {
            Configure.Component<ServiceControlBackend>(DependencyLifecycle.SingleInstance);
        }
    }
}
