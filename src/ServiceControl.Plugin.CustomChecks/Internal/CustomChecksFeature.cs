namespace ServiceControl.Plugin.CustomChecks.Internal
{
    using NServiceBus;
    using NServiceBus.Features;

    class CustomChecksFeature : Feature
    {
        public override bool IsEnabledByDefault
        {
            get { return true; }
        }

        public override void Initialize()
        {
            Configure.Instance.ForAllTypes<ICustomCheck>(
         t => Configure.Component(t, DependencyLifecycle.InstancePerCall));
            Configure.Instance.ForAllTypes<IPeriodicCheck>(
              t => Configure.Component(t, DependencyLifecycle.InstancePerCall));

        }
    }
}
