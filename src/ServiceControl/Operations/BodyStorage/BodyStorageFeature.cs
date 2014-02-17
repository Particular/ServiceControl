namespace ServiceControl.Operations.BodyStorage
{
    using NServiceBus;
    using RavenAttachments;

    public class BodyStorageFeature:NServiceBus.Features.Feature
    {
        public override bool IsEnabledByDefault
        {
            get { return true; }
        }

        public override void Initialize()
        {
            if (Configure.HasComponent<IBodyStorage>())
            {
                return;
            }

            Configure.Component<RavenAttachmentsBodyStorage>(DependencyLifecycle.SingleInstance);
        }
    }
}