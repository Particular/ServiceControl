namespace ServiceControl.Recoverability
{
    using NServiceBus;
    using NServiceBus.Features;

    public class Grouping : Feature
    {
        public Grouping()
        {
            EnableByDefault();
        }

        protected override void Setup(FeatureConfigurationContext context)
        {
            context.Container.ConfigureComponent<PublishAllHandler>(DependencyLifecycle.SingleInstance);
        }
    }
}
