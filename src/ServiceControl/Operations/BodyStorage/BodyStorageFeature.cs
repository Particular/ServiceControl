namespace ServiceControl.Operations.BodyStorage
{
    using NServiceBus;
    using NServiceBus.Features;
    using RavenAttachments;

    public class BodyStorageFeature : Feature
    {
        public BodyStorageFeature()
        {
            EnableByDefault();
        }

        protected override void Setup(FeatureConfigurationContext context)
        {
            if (!context.Container.HasComponent<IBodyStorage>())
            {
                context.Container.ConfigureComponent<RavenAttachmentsBodyStorage>(DependencyLifecycle.SingleInstance);
            }

            context.Container.ConfigureComponent<StoreBody>(DependencyLifecycle.SingleInstance);
        }
    }
}