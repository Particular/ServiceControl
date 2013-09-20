namespace ServiceControl.CustomChecks
{
    using NServiceBus;

    class RegisterCustomCheckMonitor : INeedInitialization
    {
        public void Init()
        {
            Configure.Component<CustomCheckMonitor>(DependencyLifecycle.SingleInstance);
        }
    }
}
