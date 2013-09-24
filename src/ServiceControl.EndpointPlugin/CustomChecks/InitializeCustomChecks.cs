namespace ServiceControl.EndpointPlugin.CustomChecks
{
    using Messages.CustomChecks;
    using NServiceBus;

    class InitializeCustomChecks : INeedInitialization
    {
        public void Init()
        {
            Configure.Instance.ForAllTypes<ICustomCheck>(
                t => Configure.Component(t, DependencyLifecycle.InstancePerCall));
            Configure.Instance.ForAllTypes<IPeriodicCheck>(
              t => Configure.Component(t, DependencyLifecycle.InstancePerCall));
        }
    }
}
